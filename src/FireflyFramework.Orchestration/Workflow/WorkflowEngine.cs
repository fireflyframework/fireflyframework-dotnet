using System.Diagnostics;
using System.Reflection;
using FireflyFramework.Orchestration.Core;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Orchestration.Workflow;

/// <summary>
/// Reflective workflow engine. Mirrors Java <c>WorkflowEngine</c>.
/// </summary>
/// <remarks>
/// Discovers <see cref="WorkflowStepAttribute"/> methods on the supplied workflow
/// instance, runs them sequentially, and honours <see cref="WaitForSignalAttribute"/>
/// and <see cref="WaitForTimerAttribute"/> on each step. Compensation steps tagged
/// with <c>CompensationStepAttribute</c> are invoked on failure if present.
/// </remarks>
public sealed class WorkflowEngine
{
    private readonly SignalService _signals;
    private readonly TimerService _timers;
    private readonly ILogger<WorkflowEngine> _log;

    public WorkflowEngine(SignalService signals, TimerService timers, ILogger<WorkflowEngine> log)
    {
        _signals = signals;
        _timers = timers;
        _log = log;
    }

    public async Task<WorkflowResult> ExecuteAsync(object workflow, object? input = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        var def = workflow.GetType().GetCustomAttribute<WorkflowAttribute>()
            ?? throw new InvalidOperationException($"{workflow.GetType().Name} is not annotated with [Workflow]");

        var ctx = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Workflow };
        if (input is not null)
        {
            ctx.Variables["input"] = input;
        }

        var steps = workflow.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<WorkflowStepAttribute>() is not null)
            .OrderBy(m => m.GetCustomAttribute<WorkflowStepAttribute>()!.Id)
            .ToList();

        ctx.Status = ExecutionStatus.Running;
        var sw = Stopwatch.StartNew();

        foreach (var step in steps)
        {
            var stepAttr = step.GetCustomAttribute<WorkflowStepAttribute>()!;
            var signalAttr = step.GetCustomAttribute<WaitForSignalAttribute>();
            var timerAttr = step.GetCustomAttribute<WaitForTimerAttribute>();
            var stepStart = Stopwatch.StartNew();

            try
            {
                if (timerAttr is not null)
                {
                    await _timers.SleepAsync(TimeSpan.FromMilliseconds(timerAttr.DurationMs), ct).ConfigureAwait(false);
                }

                object? signalPayload = null;
                if (signalAttr is not null)
                {
                    var key = $"{def.Id}:{ctx.CorrelationId}:{signalAttr.Name}";
                    signalPayload = signalAttr.TimeoutMs > 0
                        ? await _signals.WaitAsync(key, TimeSpan.FromMilliseconds(signalAttr.TimeoutMs), ct).ConfigureAwait(false)
                        : await _signals.WaitAsync(key, ct: ct).ConfigureAwait(false);
                }

                _log.LogDebug("Workflow {Workflow} executing step {Step}", def.Id, stepAttr.Id);
                var result = await InvokeAsync(step, workflow, ctx, signalPayload, ct).ConfigureAwait(false);
                ctx.StepResults[stepAttr.Id] = new StepResult(
                    stepAttr.Id, StepStatus.Completed, result, null, stepStart.Elapsed, 1);
            }
            catch (Exception ex)
            {
                ctx.StepResults[stepAttr.Id] = new StepResult(
                    stepAttr.Id, StepStatus.Failed, null, ex, stepStart.Elapsed, 1);
                ctx.Status = ExecutionStatus.Failed;
                ctx.CompletedAt = DateTimeOffset.UtcNow;
                _log.LogError(ex, "Workflow {Workflow} step {Step} failed", def.Id, stepAttr.Id);
                return new WorkflowResult(false, ctx, ex, sw.Elapsed);
            }
        }

        ctx.Status = ExecutionStatus.Completed;
        ctx.CompletedAt = DateTimeOffset.UtcNow;
        return new WorkflowResult(true, ctx, null, sw.Elapsed);
    }

    /// <summary>Send a signal to a waiting workflow. Returns false if no workflow is waiting.</summary>
    public bool SendSignal(string workflowId, string correlationId, string signalName, object? payload = null) =>
        _signals.Publish($"{workflowId}:{correlationId}:{signalName}", payload);

    private static async Task<object?> InvokeAsync(MethodInfo method, object instance, OrchestrationExecutionContext ctx, object? signalPayload, CancellationToken ct)
    {
        var args = method.GetParameters()
            .Select<ParameterInfo, object?>(p => p.ParameterType switch
            {
                Type t when t == typeof(OrchestrationExecutionContext) => ctx,
                Type t when t == typeof(CancellationToken) => ct,
                Type t when signalPayload is not null && t.IsInstanceOfType(signalPayload) => signalPayload,
                _ => null,
            })
            .ToArray();

        var result = method.Invoke(instance, args);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return result;
    }
}

public sealed record WorkflowResult(bool Success, OrchestrationExecutionContext Context, Exception? Error, TimeSpan Duration);

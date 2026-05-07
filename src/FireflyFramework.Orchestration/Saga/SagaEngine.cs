// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics;
using System.Reflection;
using FireflyFramework.Orchestration.Core;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Orchestration.Saga;

/// <summary>
/// Reflective saga executor. Discovers <see cref="SagaStepAttribute"/> methods on the
/// target instance and invokes them in topological order; on failure, runs the matching
/// <see cref="SagaStepAttribute.Compensate"/> methods in reverse for already-completed steps.
/// Mirrors Java <c>SagaEngine</c>.
/// </summary>
public sealed class SagaEngine
{
    private readonly ILogger<SagaEngine> _log;

    public SagaEngine(ILogger<SagaEngine> log) => _log = log;

    public async Task<SagaResult> ExecuteAsync(object sagaInstance, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sagaInstance);
        var attr = sagaInstance.GetType().GetCustomAttribute<SagaAttribute>()
            ?? throw new InvalidOperationException($"{sagaInstance.GetType().Name} is not annotated with [Saga]");

        var ctx = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Saga };
        var stepMethods = sagaInstance.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => (Method: m, Step: m.GetCustomAttribute<SagaStepAttribute>()))
            .Where(t => t.Step is not null)
            .ToList();

        var ordered = TopoSort(stepMethods.Select(s => s.Step!).ToList());
        var completed = new Stack<(MethodInfo Method, SagaStepAttribute Step)>();

        foreach (var stepId in ordered)
        {
            var (method, step) = stepMethods.First(s => s.Step!.Id == stepId);
            ctx.Status = ExecutionStatus.Running;
            var sw = Stopwatch.StartNew();
            try
            {
                _log.LogDebug("Saga {Saga} executing step {Step}", attr.Name, stepId);
                var result = await InvokeAsync(method!, sagaInstance, ctx, ct).ConfigureAwait(false);
                ctx.StepResults[stepId] = new StepResult(stepId, StepStatus.Completed, result, null, sw.Elapsed, 1);
                completed.Push((method!, step!));
            }
            catch (Exception ex)
            {
                ctx.StepResults[stepId] = new StepResult(stepId, StepStatus.Failed, null, ex, sw.Elapsed, 1);
                _log.LogError(ex, "Saga {Saga} step {Step} failed; compensating", attr.Name, stepId);
                await CompensateAsync(sagaInstance, completed, ctx, ct).ConfigureAwait(false);
                ctx.Status = ExecutionStatus.Compensating;
                ctx.CompletedAt = DateTimeOffset.UtcNow;
                return new SagaResult(false, ctx, ex);
            }
        }

        ctx.Status = ExecutionStatus.Completed;
        ctx.CompletedAt = DateTimeOffset.UtcNow;
        return new SagaResult(true, ctx, null);
    }

    private static async Task<object?> InvokeAsync(MethodInfo method, object instance, OrchestrationExecutionContext ctx, CancellationToken ct)
    {
        var args = method.GetParameters()
            .Select<ParameterInfo, object?>(p => p.ParameterType switch
            {
                Type t when t == typeof(OrchestrationExecutionContext) => ctx,
                Type t when t == typeof(CancellationToken) => ct,
                _ => null,
            })
            .ToArray();

        var result = method.Invoke(instance, args);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            var resultProp = task.GetType().GetProperty("Result");
            return resultProp?.GetValue(task);
        }

        return result;
    }

    private static async Task CompensateAsync(object instance, Stack<(MethodInfo Method, SagaStepAttribute Step)> completed,
        OrchestrationExecutionContext ctx, CancellationToken ct)
    {
        while (completed.Count > 0)
        {
            var (method, step) = completed.Pop();
            if (string.IsNullOrEmpty(step.Compensate)) continue;
            var compensateMethod = instance.GetType().GetMethod(step.Compensate,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (compensateMethod is null) continue;
            try
            {
                await InvokeAsync(compensateMethod, instance, ctx, ct).ConfigureAwait(false);
                if (ctx.StepResults.TryGetValue(step.Id, out var prev))
                {
                    ctx.StepResults[step.Id] = prev with { Status = StepStatus.Compensated };
                }
            }
            catch (Exception)
            {
                if (ctx.StepResults.TryGetValue(step.Id, out var prev))
                {
                    ctx.StepResults[step.Id] = prev with { Status = StepStatus.CompensationFailed };
                }
            }
        }
    }

    private static List<string> TopoSort(IReadOnlyList<SagaStepAttribute> steps)
    {
        var ordered = new List<string>();
        var visited = new HashSet<string>();
        var byId = steps.ToDictionary(s => s.Id);

        void Visit(SagaStepAttribute step)
        {
            if (!visited.Add(step.Id)) return;
            foreach (var dep in step.DependsOn)
            {
                if (byId.TryGetValue(dep, out var depStep)) Visit(depStep);
            }

            ordered.Add(step.Id);
        }

        foreach (var s in steps) Visit(s);
        return ordered;
    }
}

public sealed record SagaResult(bool Success, OrchestrationExecutionContext Context, Exception? Error);

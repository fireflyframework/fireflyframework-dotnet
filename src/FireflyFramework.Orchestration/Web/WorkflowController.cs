using FireflyFramework.Orchestration.Core;
using FireflyFramework.Orchestration.Workflow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Orchestration.Web;

/// <summary>
/// REST API for workflow lifecycle operations. Mirrors Java <c>WorkflowController</c>.
/// Mounted at <c>/api/orchestration/workflows</c>; exposes:
///
/// <list type="bullet">
/// <item><c>GET    /</c> — list every registered workflow definition.</item>
/// <item><c>GET    /{workflowId}</c> — describe one workflow definition.</item>
/// <item><c>POST   /{workflowId}/start</c> — synchronously execute a workflow with the supplied input.</item>
/// <item><c>POST   /instances/{correlationId}/cancel</c> — request cancellation.</item>
/// <item><c>POST   /instances/{correlationId}/suspend</c> — pause an active execution.</item>
/// <item><c>POST   /instances/{correlationId}/resume</c> — resume a suspended execution.</item>
/// <item><c>POST   /instances/{correlationId}/signal/{signalName}</c> — deliver a signal to a waiting workflow.</item>
/// <item><c>GET    /instances/{correlationId}</c> — fetch execution state.</item>
/// </list>
///
/// <para>Workflows must be registered in <see cref="WorkflowRegistry"/> at host startup
/// (e.g. via <c>registry.RegisterFromAssembly(typeof(Program).Assembly)</c>).</para>
/// </summary>
[ApiController]
[Route("api/orchestration/workflows")]
public sealed class WorkflowController : ControllerBase
{
    private readonly WorkflowRegistry _registry;
    private readonly WorkflowEngine _engine;
    private readonly WorkflowLifecycleService _lifecycle;
    private readonly IServiceProvider _services;

    public WorkflowController(
        WorkflowRegistry registry,
        WorkflowEngine engine,
        WorkflowLifecycleService lifecycle,
        IServiceProvider services)
    {
        _registry = registry;
        _engine = engine;
        _lifecycle = lifecycle;
        _services = services;
    }

    [HttpGet]
    public IReadOnlyList<WorkflowDescriptor> ListWorkflows() => _registry.GetAll();

    [HttpGet("{workflowId}")]
    public ActionResult<WorkflowDescriptor> Describe(string workflowId)
    {
        var descriptor = _registry.Describe(workflowId);
        return descriptor is null ? NotFound() : descriptor;
    }

    [HttpPost("{workflowId}/start")]
    public async Task<ActionResult<StartWorkflowResponse>> Start(string workflowId, [FromBody] StartWorkflowRequest? request, CancellationToken ct)
    {
        var type = _registry.Find(workflowId);
        if (type is null) return NotFound($"Workflow '{workflowId}' is not registered.");

        var instance = ActivatorUtilities.CreateInstance(_services, type);
        var result = await _engine.ExecuteAsync(instance, request?.Input, ct).ConfigureAwait(false);

        return new StartWorkflowResponse(
            result.Context.CorrelationId,
            result.Context.Status,
            result.Success,
            result.Error?.Message,
            result.Duration);
    }

    [HttpPost("instances/{correlationId}/cancel")]
    public async Task<ActionResult<bool>> Cancel(string correlationId, CancellationToken ct)
    {
        var ok = await _lifecycle.CancelAsync(correlationId, ct).ConfigureAwait(false);
        return ok ? Ok(true) : NotFound();
    }

    [HttpPost("instances/{correlationId}/suspend")]
    public async Task<ActionResult<bool>> Suspend(string correlationId, CancellationToken ct)
    {
        var ok = await _lifecycle.SuspendAsync(correlationId, ct).ConfigureAwait(false);
        return ok ? Ok(true) : NotFound();
    }

    [HttpPost("instances/{correlationId}/resume")]
    public async Task<ActionResult<bool>> Resume(string correlationId, CancellationToken ct)
    {
        var ok = await _lifecycle.ResumeAsync(correlationId, ct).ConfigureAwait(false);
        return ok ? Ok(true) : NotFound();
    }

    [HttpPost("instances/{correlationId}/signal/{signalName}")]
    public ActionResult<SignalResponse> Signal(string correlationId, string signalName, [FromQuery] string workflowId, [FromBody] object? payload)
    {
        var delivered = _engine.SendSignal(workflowId, correlationId, signalName, payload);
        return new SignalResponse(delivered, payload);
    }

    [HttpGet("instances/{correlationId}")]
    public async Task<ActionResult<OrchestrationExecutionContext>> GetInstance(string correlationId, CancellationToken ct)
    {
        var ctx = await _lifecycle.GetAsync(correlationId, ct).ConfigureAwait(false);
        return ctx is null ? NotFound() : ctx;
    }
}

/// <summary>POST body for <c>/{workflowId}/start</c>.</summary>
public sealed record StartWorkflowRequest(object? Input);

/// <summary>Response shape from <c>/{workflowId}/start</c>.</summary>
public sealed record StartWorkflowResponse(
    string CorrelationId,
    ExecutionStatus Status,
    bool Success,
    string? Error,
    TimeSpan Duration);

/// <summary>Response shape from <c>/instances/{correlationId}/signal/{signalName}</c>.</summary>
public sealed record SignalResponse(bool Delivered, object? Payload);

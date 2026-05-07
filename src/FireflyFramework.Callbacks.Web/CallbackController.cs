using FireflyFramework.Callbacks.Core;
using FireflyFramework.Callbacks.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FireflyFramework.Callbacks.Web;

[ApiController]
[Route("api/callbacks/configurations")]
public sealed class CallbackConfigurationController : ControllerBase
{
    private readonly ICallbackConfigurationStore _store;

    public CallbackConfigurationController(ICallbackConfigurationStore store) => _store = store;

    [HttpGet]
    public async Task<IReadOnlyList<CallbackConfigurationDto>> List(
        [FromQuery] string? tenantId = null, CancellationToken ct = default) =>
        await _store.ListAsync(tenantId, ct).ConfigureAwait(false);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CallbackConfigurationDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await _store.GetAsync(id, ct).ConfigureAwait(false);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CallbackConfigurationDto>> Create(
        [FromBody] CallbackConfigurationDto dto, CancellationToken ct)
    {
        var stored = await _store.CreateAsync(dto, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(Get), new { id = stored.Id }, stored);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CallbackConfigurationDto>> Update(
        Guid id, [FromBody] CallbackConfigurationDto dto, CancellationToken ct)
    {
        var updated = await _store.UpdateAsync(id, dto, ct).ConfigureAwait(false);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _store.DeleteAsync(id, ct).ConfigureAwait(false) ? NoContent() : NotFound();
}

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

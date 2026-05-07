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

using FireflyFramework.Orchestration.Core;
using FireflyFramework.Orchestration.DeadLetter;
using Microsoft.AspNetCore.Mvc;

namespace FireflyFramework.Orchestration.Web;

/// <summary>
/// REST control plane for the orchestration dead-letter queue. Mirrors Java
/// <c>DeadLetterController</c>. Mounted at <c>/api/orchestration/dlq</c>; exposes:
///
/// <list type="bullet">
/// <item><c>GET  /</c> — list dead-letter entries, optionally filtered by pattern.</item>
/// <item><c>GET  /count</c> — total entry count.</item>
/// <item><c>GET  /{id}</c> — fetch one entry by id.</item>
/// <item><c>DELETE /{id}</c> — discard one entry.</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/orchestration/dlq")]
public sealed class DeadLetterController : ControllerBase
{
    private readonly IDeadLetterStore _store;

    public DeadLetterController(IDeadLetterStore store)
    {
        _store = store;
    }

    [HttpGet]
    public Task<IReadOnlyList<DeadLetterEntry>> List([FromQuery] ExecutionPattern? pattern, [FromQuery] int limit = 100, CancellationToken ct = default) =>
        _store.ListAsync(pattern, limit, ct);

    [HttpGet("count")]
    public async Task<int> Count(CancellationToken ct = default) => (await _store.ListAsync(null, int.MaxValue, ct).ConfigureAwait(false)).Count;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeadLetterEntry>> Get(Guid id, CancellationToken ct = default)
    {
        var entry = await _store.GetAsync(id, ct).ConfigureAwait(false);
        return entry is null ? NotFound() : entry;
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<bool>> Delete(Guid id, CancellationToken ct = default)
    {
        var removed = await _store.RemoveAsync(id, ct).ConfigureAwait(false);
        return removed ? Ok(true) : NotFound();
    }
}

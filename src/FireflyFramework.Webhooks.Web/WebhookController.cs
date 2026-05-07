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

using System.Text.Json;
using FireflyFramework.Webhooks.Core;
using FireflyFramework.Webhooks.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FireflyFramework.Webhooks.Web;

[ApiController]
[Route("api/webhooks/{provider}")]
public sealed class WebhookController : ControllerBase
{
    private readonly IWebhookProcessingService _service;

    public WebhookController(IWebhookProcessingService service) => _service = service;

    [HttpPost]
    public async Task<WebhookResponseDto> Ingest(string provider, [FromBody] JsonElement payload, CancellationToken ct)
    {
        var headers = HttpContext.Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString());
        var query = HttpContext.Request.Query
            .ToDictionary(q => q.Key, q => q.Value.ToString());
        var dto = new WebhookEventDto(
            Guid.NewGuid().ToString(),
            provider,
            payload,
            headers,
            query,
            DateTimeOffset.UtcNow,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Method);

        return await _service.ProcessAsync(dto, ct).ConfigureAwait(false);
    }
}

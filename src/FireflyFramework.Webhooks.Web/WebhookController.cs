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

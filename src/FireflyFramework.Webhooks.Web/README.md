# FireflyFramework.Webhooks.Web

ASP.NET Core ingestion controller for inbound webhooks. Receives the
raw provider payload, builds a `WebhookEventDto`, and dispatches it to
`IWebhookProcessingService` from `Webhooks.Core` for the full
validate → rate-limit → enrich → dispatch → DLQ pipeline.

Mirrors `org.fireflyframework:firefly-webhooks-web`.

## Endpoint

| Method | Path                                | Body          | Description                                  |
|--------|-------------------------------------|---------------|----------------------------------------------|
| POST   | `/api/webhooks/{provider}`          | JSON object   | Ingest a webhook event from `{provider}`     |

The controller forwards the raw body, headers, query string, source IP,
and HTTP method into a `WebhookEventDto` and returns the
`WebhookResponseDto` produced by the pipeline (`EventId`, `Status`,
`Message?`, `ProcessingTimeMs`).

## Wiring

```csharp
using FireflyFramework.Webhooks.Core;
using FireflyFramework.Webhooks.Web;

builder.Services.AddSingleton<IWebhookProcessingService, WebhookProcessingService>();
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(WebhookController).Assembly);
```

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.Webhooks.Core`         | `IWebhookProcessingService`         |
| `FireflyFramework.Webhooks.Interfaces`   | DTOs                                |
| `Microsoft.AspNetCore.App`               | `[ApiController]`, MVC binding      |

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `WebhookController`   | `WebhookController`               |

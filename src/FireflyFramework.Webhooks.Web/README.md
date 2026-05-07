# FireflyFramework.Webhooks.Web

ASP.NET Core ingestion controller for inbound webhooks. Receives raw
provider payloads, builds a `WebhookEventDto`, and dispatches it to
`IWebhookProcessingService` from `Webhooks.Core` for the full
validate-rate-limit-enrich-dispatch pipeline.

Mirrors `org.fireflyframework:firefly-webhooks-web`.

## Endpoint

| Method | Path                                | Description                                      |
|--------|-------------------------------------|--------------------------------------------------|
| POST   | `/api/webhooks/{provider}`          | Ingest a webhook event from the named provider   |

The controller passes the raw body, headers, query string, source IP,
and HTTP method into a `WebhookEventDto` and returns the
`WebhookResponseDto` produced by the pipeline.

## Wiring

```csharp
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
| `Microsoft.AspNetCore.App` (FrameworkRef)| ApiController, MVC binding          |

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `WebhookController`   | `WebhookController`               |

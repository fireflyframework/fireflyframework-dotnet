# FireflyFramework.Webhooks.Sdk

Typed `HttpClient` for the webhook-ingestion endpoint exposed by
`FireflyFramework.Webhooks.Web`. Use it from any .NET service that
needs to forward an inbound webhook event to the framework's ingestion
pipeline.

Mirrors `org.fireflyframework:firefly-webhooks-sdk`.

## Wiring

```csharp
using FireflyFramework.Webhooks.Sdk;

builder.Services.AddWebhookClient(new Uri("https://webhooks.svc.local"));
```

`AddWebhookClient` registers `IWebhookClient` against a typed
`HttpClient` — the same shape as the canonical service Sdk in
[`samples/FireflyFramework.Samples.OrdersService.Sdk`](../../samples/FireflyFramework.Samples.OrdersService.Sdk).

## Usage

```csharp
public sealed class StripeWebhookForwarder(IWebhookClient client)
{
    public Task<WebhookResponseDto?> Forward(object stripeEvent, CancellationToken ct) =>
        client.SendAsync("stripe", stripeEvent, ct);
}
```

## Public surface

| Member                                        | Calls                                                |
|-----------------------------------------------|------------------------------------------------------|
| `IWebhookClient.SendAsync(provider, payload)` | `POST /api/webhooks/{provider}`                      |
| `AddWebhookClient(IServiceCollection, Uri)`   | Registers `IWebhookClient` + `WebhookClient`         |

`SendAsync` URL-encodes `provider`, posts `payload` as JSON, and returns
the framework's `WebhookResponseDto` (`EventId`, `Status`, `Message?`,
`ProcessingTimeMs`). Non-success responses throw `HttpRequestException`
via `EnsureSuccessStatusCode`.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.Webhooks.Interfaces`   | DTO shapes                          |
| `Microsoft.Extensions.Http`              | `AddHttpClient<TClient, TImpl>`     |

`System.Net.Http.Json` ships in the .NET 10 framework — no package
import needed.

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `IWebhookClient`      | `WebhookClient` (interface)       |
| `WebhookClient`       | `WebhookClient`                   |
| `AddWebhookClient`    | Spring Cloud OpenFeign auto-config |

# FireflyFramework.Webhooks.Sdk

Typed `HttpClient` wrapper for posting webhook events to the ingestion
endpoint exposed by `FireflyFramework.Webhooks.Web`.

Mirrors `org.fireflyframework:firefly-webhooks-sdk`.

## Usage

```csharp
using FireflyFramework.Webhooks.Sdk;

builder.Services
    .AddHttpClient<WebhookClient>(c => c.BaseAddress = new Uri("https://webhooks.svc.local"));

var response = await client.SendAsync(provider: "stripe", evt, ct);
```

## Public surface

| Method                   | Calls                             |
|--------------------------|-----------------------------------|
| `SendAsync(provider, evt)` | `POST /api/webhooks/{provider}` |

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Webhooks.Interfaces`   | DTOs                           |
| `System.Net.Http.Json`                   | Typed JSON HTTP                |

## Java mapping

| .NET             | Java                              |
|------------------|-----------------------------------|
| `WebhookClient`  | `WebhookClient`                   |

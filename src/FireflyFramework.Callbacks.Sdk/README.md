# FireflyFramework.Callbacks.Sdk

## Overview

`FireflyFramework.Callbacks.Sdk` is a **typed `HttpClient`** for the
callback-management REST API exposed by
`FireflyFramework.Callbacks.Web`. Use it from any .NET service that
needs to register, list, update, or delete outbound-callback
configurations remotely without taking the dispatch runtime
(`Callbacks.Core`).

The bundle this SDK pairs with is the *callback configuration
management* surface — not the dispatch surface. The dispatch engine
(HMAC signing, Polly retry, audit log) runs inside the host service
that *owns* the callbacks; consumers of this SDK are typically admin
UIs, configuration migration scripts, or sibling services that
provision callbacks programmatically.

Mirrors `org.fireflyframework:firefly-callbacks-sdk` from the Java
line.

## Why a separate module?

The Java line splits the callback subsystem into five tiers
(`interfaces`, `models`, `core`, `web`, `sdk`); the .NET port
preserves the split. The SDK depends only on
`Callbacks.Interfaces` (DTO shapes) — pulling it in does *not* drag
in the dispatch runtime, the EF Core entities, or the ASP.NET
controller. A 30 KB import gets you typed CRUD over the callback
configuration API.

## Mental model

```
   admin UI / migration / sibling service
        │
        │ wants to manage callback configs
        ▼
   ICallbackClient ─── HttpClient ───► /api/callbacks/configurations/*
   (this module)            │
                            │
                    message-handler pipeline:
                    ├── correlation-id header
                    ├── auth header (operator's role token)
                    ├── Polly retry
                    └── OpenTelemetry span
```

Every method on `ICallbackClient` corresponds to one REST endpoint;
the SDK is intentionally a thin transport layer. Add resilience and
auth via `IHttpClientBuilder` extensions — the typical typed-client
pattern.

## Quick start

```csharp
using FireflyFramework.Callbacks.Sdk;

builder.Services.AddCallbackClient(new Uri("https://callbacks.svc.local"));
```

Then inject `ICallbackClient`:

```csharp
public sealed class CallbackAdminPage(ICallbackClient client)
{
    public async Task<IReadOnlyList<CallbackConfigurationDto>?> Index(string? tenantId, CancellationToken ct) =>
        await client.ListAsync(tenantId, ct);

    public Task<CallbackConfigurationDto?> Create(CallbackConfigurationDto dto, CancellationToken ct) =>
        client.CreateAsync(dto, ct);

    public Task<bool> Delete(Guid id, CancellationToken ct) =>
        client.DeleteAsync(id, ct);
}
```

## Public surface

| Member                                       | Calls                                                       |
|----------------------------------------------|-------------------------------------------------------------|
| `ICallbackClient.ListAsync(tenantId?)`       | `GET /api/callbacks/configurations[?tenantId=]`             |
| `ICallbackClient.GetAsync(id)`               | `GET /api/callbacks/configurations/{id}` (`null` on 404)    |
| `ICallbackClient.CreateAsync(dto)`           | `POST /api/callbacks/configurations`                        |
| `ICallbackClient.UpdateAsync(id, dto)`       | `PUT /api/callbacks/configurations/{id}` (`null` on 404)    |
| `ICallbackClient.DeleteAsync(id)`            | `DELETE /api/callbacks/configurations/{id}` (`false` on 404)|
| `AddCallbackClient(IServiceCollection, Uri)` | Registers `ICallbackClient` + `CallbackClient`              |

All methods accept a trailing `CancellationToken`. Non-404 non-success
responses throw `HttpRequestException` via
`EnsureSuccessStatusCode`. The 404 cases are returned as `null` /
`false` so the typical "not found" path doesn't need a try/catch.

## Common patterns

### Provisioning a callback at deployment time

```csharp
public sealed class CallbackProvisionerHostedService(
    ICallbackClient client,
    IConfiguration cfg,
    ILogger<CallbackProvisionerHostedService> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var existing = await client.ListAsync(tenantId: null, ct);
        if (existing!.Any(c => c.Name == "OrderEvents")) return;

        await client.CreateAsync(new CallbackConfigurationDto(
            Id:                       null,
            Name:                     "OrderEvents",
            Url:                      cfg["Partner:CallbackUrl"]!,
            HttpMethod:               CallbackHttpMethod.Post,
            Status:                   CallbackStatus.Active,
            SubscribedEventTypes:     new[] { "order.created", "order.shipped" },
            CustomHeaders:            null,
            Secret:                   cfg["Partner:HmacSecret"]!,
            SignatureEnabled:         true,
            SignatureHeader:          "X-Signature",
            MaxRetries:               5,
            RetryDelayMs:             500,
            RetryBackoffMultiplier:   2.0,
            TimeoutMs:                10_000,
            Active:                   true,
            TenantId:                 null,
            FilterExpression:         null,
            Metadata:                 null,
            FailureThreshold:         20,
            FailureCount:             0,
            LastSuccessAt:            null,
            LastFailureAt:            null,
            CreatedAt:                DateTimeOffset.UtcNow,
            UpdatedAt:                null,
            CreatedBy:                "deploy-script",
            UpdatedBy:                null), ct);

        log.LogInformation("Provisioned OrderEvents callback");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### Disabling a noisy callback

```csharp
public async Task SuspendAsync(Guid id, string reason, CancellationToken ct)
{
    var current = await client.GetAsync(id, ct);
    if (current is null) return;

    var paused = current with
    {
        Status    = CallbackStatus.Paused,
        Active    = false,
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedBy = "ops-runbook",
    };
    await client.UpdateAsync(id, paused, ct);
}
```

`with { … }` returns a fresh record (records are immutable). The
update endpoint accepts the entire DTO — partial updates are not
supported intentionally so the wire shape stays simple.

### Layering resilience and auth

```csharp
builder.Services.AddCallbackClient(new Uri("https://callbacks.svc.local"))
    .AddStandardResilienceHandler()
    .AddHttpMessageHandler(sp =>
    {
        var tokens = sp.GetRequiredService<IOAuth2TokenCache>();
        return new BearerTokenHandler(tokens, audience: "callbacks-api");
    });
```

## Pitfalls and gotchas

- **`UpdateAsync` is full-replace.** The DTO you pass replaces the
  stored record. Always read with `GetAsync` first, then mutate via
  `with { … }`, then `UpdateAsync`. Sending a partially-populated DTO
  will null out the missing fields.
- **`ListAsync(tenantId)` returns *all* callbacks for that tenant.**
  No pagination on the SDK surface. For large lists, fork the SDK to
  add a `pageSize` / `pageNumber` parameter that the controller
  already supports under the hood (or add it).
- **Secret rotation needs a roundtrip.** To change `Secret`, fetch
  the current DTO, replace `Secret`, update. The framework doesn't
  expose a "rotate secret" endpoint — by design, since rotation is
  rare and explicit.
- **`FailureCount` is read-only over the wire.** Setting it via
  update is silently ignored — the dispatch engine maintains it.
- **`CreatedBy` / `UpdatedBy` are operator-supplied.** The framework
  doesn't infer them from the auth token. Set a meaningful value
  (the operator's email or a deployment id).

## Internals (for the curious)

- `CallbackClient` uses `PostAsJsonAsync`, `PutAsJsonAsync`, etc.
  for serialisation — the framework's default
  `JsonSerializerOptions` are sufficient for the `Dto` records.
- 404 handling: the client checks `response.StatusCode ==
  HttpStatusCode.NotFound` *before* calling
  `EnsureSuccessStatusCode`. That way, "not found" surfaces as a
  return value rather than an exception.
- `AddCallbackClient` returns `IHttpClientBuilder` so the caller
  can compose handlers.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.Callbacks.Interfaces`  | DTO shapes                          |
| `Microsoft.Extensions.Http`              | `AddHttpClient<TClient, TImpl>`     |

`System.Net.Http.Json` ships in the .NET framework — no package
import needed.

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `ICallbackClient`             | `CallbackClient` (interface)        |
| `CallbackClient`              | `CallbackClient`                    |
| `AddCallbackClient`           | Spring Cloud OpenFeign auto-config  |

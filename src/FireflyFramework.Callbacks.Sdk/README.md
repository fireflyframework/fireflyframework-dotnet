# FireflyFramework.Callbacks.Sdk

Typed `HttpClient` for the callback-management REST API exposed by
`FireflyFramework.Callbacks.Web`. Use it from any .NET service that
needs to register, list, update, or delete callbacks remotely.

Mirrors `org.fireflyframework:firefly-callbacks-sdk`.

## Wiring

```csharp
using FireflyFramework.Callbacks.Sdk;

builder.Services.AddCallbackClient(new Uri("https://callbacks.svc.local"));
```

`AddCallbackClient` registers `ICallbackClient` against a typed
`HttpClient` — the same shape as the canonical service Sdk in
[`samples/FireflyFramework.Samples.OrdersService.Sdk`](../../samples/FireflyFramework.Samples.OrdersService.Sdk).

## Usage

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
responses throw `HttpRequestException` via `EnsureSuccessStatusCode`.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.Callbacks.Interfaces`  | DTO shapes                          |
| `Microsoft.Extensions.Http`              | `AddHttpClient<TClient, TImpl>`     |

`System.Net.Http.Json` ships in the .NET 10 framework — no package
import needed.

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `ICallbackClient`             | `CallbackClient` (interface)        |
| `CallbackClient`              | `CallbackClient`                    |
| `AddCallbackClient`           | Spring Cloud OpenFeign auto-config  |

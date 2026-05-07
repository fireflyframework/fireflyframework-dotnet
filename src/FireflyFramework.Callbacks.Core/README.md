# FireflyFramework.Callbacks.Core

Outbound-callback runtime. Dispatches HTTP callbacks with HMAC signing
and Polly retry, persists configurations and execution audit logs,
authorises destination domains, and routes events to every subscribed
callback.

Mirrors `org.fireflyframework:firefly-callbacks-core`.

## Public surface

### `ICallbackDispatcher` / `CallbackDispatcher`

Dispatches a single configured callback for a single event. Polly
pipeline: retry with exponential backoff and jitter, then timeout.
HMAC-SHA256 signature is added to the request when
`Configuration.SignatureEnabled` is true and a `Secret` is set.

```csharp
var execution = await dispatcher.DispatchAsync(
    config:    callbackConfig,
    eventType: "order.created",
    payload:   "{\"orderId\":42}",
    ct:        ct);
```

### `ICallbackConfigurationStore`

Persistence-agnostic CRUD for `CallbackConfigurationDto`:

```csharp
public interface ICallbackConfigurationStore
{
    Task<IReadOnlyList<CallbackConfigurationDto>> ListAsync(string? tenantId = null, CancellationToken ct = default);
    Task<CallbackConfigurationDto?>               GetAsync (Guid id, CancellationToken ct = default);
    Task<CallbackConfigurationDto>                CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<CallbackConfigurationDto?>               UpdateAsync(Guid id, CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<bool>                                    DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CallbackConfigurationDto>> FindBySubscribedEventAsync(string eventType, string? tenantId = null, CancellationToken ct = default);
}
```

`InMemoryCallbackConfigurationStore` is the default.

### `ICallbackRouter` / `CallbackRouter`

Receives a domain event and dispatches it to every active subscribed
callback. Optionally validates each destination URL through
`IDomainAuthorizationService` and persists each execution through
`ICallbackExecutionStore`.

```csharp
var executions = await router.RouteAsync(
    eventType: "order.created",
    payload:   "{\"orderId\":42}",
    tenantId:  "alpha");
```

### `ICallbackExecutionStore`

Audit log of every dispatch.

| Method                              | Purpose                                              |
|-------------------------------------|------------------------------------------------------|
| `RecordAsync`                       | Append one execution                                 |
| `ListByConfigurationAsync`          | Newest first, configurable limit                     |
| `ListByStatusAsync`                 | Filter by `Success` / `FailedRetrying` / `FailedPermanent` |
| `GetAsync`                          | Read one execution by id                             |

### `IDomainAuthorizationService`

URL allow-list with sub-domain matching:

```csharp
public interface IDomainAuthorizationService
{
    Task<bool> IsAuthorizedAsync(string url, CancellationToken ct = default);
    Task       AuthorizeAsync(AuthorizedDomainDto domain, CancellationToken ct = default);
    Task       RevokeAsync(string domain, CancellationToken ct = default);
    Task<IReadOnlyList<AuthorizedDomainDto>> ListAsync(CancellationToken ct = default);
}
```

If no domains are configured the service authorises every URL (open
default); add at least one entry to switch to deny-by-default mode.

### `IEventSubscriptionService`

Manages the `(configurationId, eventType)` subscription map.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Callbacks.Models`      | EF Core entities               |
| `FireflyFramework.Eda`                   | Optional EDA-driven dispatch   |
| `Polly` / `Polly.Core`                   | Retry pipeline                 |

## Java mapping

| .NET                                | Java                                  |
|-------------------------------------|---------------------------------------|
| `CallbackDispatcher`                | `CallbackDispatcherImpl`              |
| `ICallbackConfigurationStore`       | `CallbackConfigurationRepository`     |
| `CallbackRouter`                    | `CallbackRouterImpl`                  |
| `ICallbackExecutionStore`           | `CallbackExecutionRepository`         |
| `IDomainAuthorizationService`       | `DomainAuthorizationService`          |
| `IEventSubscriptionService`         | `EventSubscriptionServiceImpl`        |

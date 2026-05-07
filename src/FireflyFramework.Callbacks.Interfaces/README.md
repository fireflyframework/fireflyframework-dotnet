# FireflyFramework.Callbacks.Interfaces

## Overview

`FireflyFramework.Callbacks.Interfaces` is the **public contract module** for the
Firefly outbound-callback subsystem. It is a tiny, dependency-free assembly
that ships nothing but DTOs (record types) and enums describing the wire
format used by the callback REST API and the dispatch runtime.

In Firefly's hub-and-spoke architecture, the *interfaces* tier of every
multi-project service plays the same role: it is the only assembly that
external callers reference. Other services that need to talk to a Firefly
callback service over HTTP — directly or via the typed `Callbacks.Sdk` — pull
in this assembly to deserialize responses and craft requests, but they never
load `Callbacks.Core` (which carries the dispatch engine, Polly pipeline,
HMAC signing, and a few in-memory stubs that are useful for tests but should
never be transitively dragged into a consumer).

This module mirrors `org.fireflyframework:firefly-callbacks-interfaces` from
the Java line. The DTO names are intentionally one-to-one with the Java
records, with the only superficial change being the `Dto` suffix instead of
the Java `DTO` (a .NET convention).

The four enums and four records here cover the entire externally-visible
state machine of the subsystem: the lifecycle of a configuration
(`CallbackStatus`), the lifecycle of a single dispatch
(`CallbackExecutionStatus`), the supported HTTP verbs
(`CallbackHttpMethod`), the configuration record itself, the per-dispatch
audit row, the URL allow-list entry, and the `(configurationId, eventType)`
subscription tuple.

## When to use this module

Reference `FireflyFramework.Callbacks.Interfaces` from any assembly that:

- Needs the `CallbackConfigurationDto` shape because it consumes the typed
  `ICallbackClient` from `FireflyFramework.Callbacks.Sdk` (which transitively
  exposes these records as parameter and return types).
- Implements its own custom `ICallbackConfigurationStore` or
  `ICallbackExecutionStore` against a real database (EF Core, Dapper) and
  needs the DTO shape that `Callbacks.Core` and `Callbacks.Web` will move
  in and out of it.
- Subscribes to or publishes domain events that contain callback-related
  payloads, e.g. an EDA event whose body is a `CallbackExecutionDto`.

Do **not** reference this module if all you want is to render a callback
configuration in a UI without involving the framework — the records carry
framework-specific enums and would force you to import this assembly
unnecessarily.

## Mental model

Every other tier in the callback module composes on top of these DTOs:

```
Interfaces ◄── Models   (entities; mapping target)
       ▲       ▲
       │       │
       └────── Core     (services, dispatcher, router)
                   ▲
                   │
                  Web   (REST controller; serialises these DTOs over JSON)

Interfaces ◄────── Sdk  (typed HttpClient; consumes these DTOs over JSON)
```

The compiler enforces the dependency direction: every `csproj` only
references the tier directly below it, and `Interfaces` has no project
references at all. That is what makes it safe for the SDK to reference only
this assembly without dragging in EF Core, Polly, or ASP.NET.

## Public surface

### Enums

| Type                       | Values                                                | Used in                                              |
|----------------------------|-------------------------------------------------------|------------------------------------------------------|
| `CallbackStatus`           | `Active`, `Paused`, `Disabled`, `Failed`              | `CallbackConfigurationDto.Status`                    |
| `CallbackExecutionStatus`  | `Success`, `FailedRetrying`, `FailedPermanent`        | `CallbackExecutionDto.Status`                        |
| `CallbackHttpMethod`       | `Post`, `Put`, `Patch`                                | `CallbackConfigurationDto.HttpMethod`                |

`CallbackStatus.Failed` is the soft-disabled state the dispatcher transitions
into once `FailureCount` exceeds `FailureThreshold`. Operators are expected
to triage and either flip back to `Active` or `Disabled`.

`CallbackExecutionStatus.FailedRetrying` is reported when the upstream
returned a non-2xx response but the dispatcher will be retrying (Polly is in
the middle of its retry pipeline). `FailedPermanent` only fires once the
retry budget is exhausted.

### `CallbackConfigurationDto`

The entity that controls everything about an outbound callback. The shape
is:

```csharp
public sealed record CallbackConfigurationDto(
    Guid?                          Id,
    string                         Name,
    string                         Url,
    CallbackHttpMethod             HttpMethod,
    CallbackStatus                 Status,
    string[]                       SubscribedEventTypes,
    Dictionary<string, string>?    CustomHeaders,
    string?                        Secret,
    bool                           SignatureEnabled,
    string?                        SignatureHeader,
    int                            MaxRetries,
    int                            RetryDelayMs,
    double                         RetryBackoffMultiplier,
    int                            TimeoutMs,
    bool                           Active,
    string?                        TenantId,
    string?                        FilterExpression,
    Dictionary<string, object?>?   Metadata,
    int                            FailureThreshold,
    int                            FailureCount,
    DateTimeOffset?                LastSuccessAt,
    DateTimeOffset?                LastFailureAt,
    DateTimeOffset                 CreatedAt,
    DateTimeOffset?                UpdatedAt,
    string?                        CreatedBy,
    string?                        UpdatedBy);
```

A few subtleties to know about:

- `Id` is nullable on the wire because clients POST a configuration without
  knowing the id; the server assigns one and returns it.
- `Active` and `Status` are intentionally separate. `Active = false`
  short-circuits dispatch entirely; `Status` is a richer lifecycle marker
  the dispatcher uses to fail-fast or alert.
- `SubscribedEventTypes` is a flat string array. Wildcards and CEL-style
  filtering are layered on top via `FilterExpression` rather than baked into
  the subscription list.
- `RetryBackoffMultiplier` is a `double`, not a `decimal`, because the Polly
  pipeline expects floating-point values.

### `CallbackExecutionDto`

Per-dispatch audit row written by `ICallbackExecutionStore`. Captures the
inputs (`RequestPayload`, `RequestHeaders`), the outputs (`ResponseStatusCode`,
`ResponseBody`), the latency (`RequestDurationMs`), the attempt counter, and
the error message (when applicable). Most production deployments persist
these into a partitioned audit table and surface them on a dashboard.

### `AuthorizedDomainDto`

```csharp
public sealed record AuthorizedDomainDto(string Domain, string[]? AllowedIPs, bool IsAuthorized);
```

Used by `IDomainAuthorizationService` (in `Callbacks.Core`) to decide whether
the dispatcher is allowed to call out to a given URL. The `IsAuthorized`
flag lets you blacklist a domain without deleting its row.

### `EventSubscriptionDto`

```csharp
public sealed record EventSubscriptionDto(Guid ConfigurationId, string EventType, bool IsActive);
```

A flat denormalisation of `CallbackConfigurationDto.SubscribedEventTypes`
that lets you index the join table on `(EventType, IsActive)` for fast
fan-out lookup.

## Configuration

This module exposes no `IOptions<T>` — it is data-only.

## Common patterns

### Building a configuration to POST

```csharp
var config = new CallbackConfigurationDto(
    Id: null,
    Name: "OrderEvents",
    Url: "https://partner.example.com/hooks/orders",
    HttpMethod: CallbackHttpMethod.Post,
    Status: CallbackStatus.Active,
    SubscribedEventTypes: new[] { "order.created", "order.shipped" },
    CustomHeaders: new Dictionary<string, string> { ["x-tenant"] = "alpha" },
    Secret: "super-secret-shared-with-partner",
    SignatureEnabled: true,
    SignatureHeader: "X-Signature",
    MaxRetries: 5,
    RetryDelayMs: 500,
    RetryBackoffMultiplier: 2.0,
    TimeoutMs: 10_000,
    Active: true,
    TenantId: "alpha",
    FilterExpression: null,
    Metadata: null,
    FailureThreshold: 20,
    FailureCount: 0,
    LastSuccessAt: null,
    LastFailureAt: null,
    CreatedAt: DateTimeOffset.UtcNow,
    UpdatedAt: null,
    CreatedBy: "sysadmin",
    UpdatedBy: null);
```

### Inspecting an execution audit row

```csharp
if (execution.Status == CallbackExecutionStatus.FailedPermanent)
{
    logger.LogError("Callback {Id} ultimately failed after {Attempts} attempts: {Error}",
        execution.Id, execution.AttemptNumber, execution.ErrorMessage);
}
```

## Pitfalls and gotchas

- **Records are value-equal.** If you mutate a `CallbackConfigurationDto`
  by passing it through `with { ... }`, the new instance is a different
  object even though all fields might be equal — store the *result* of the
  `with` expression rather than relying on reference equality.
- **`SubscribedEventTypes` is a `string[]`.** Tests in
  `StubFixesTests.CallbackStore_filters_by_tenant_and_event` rely on
  `Contains(eventType)` exactly. Watch for case mismatches; the framework
  uses an ordinal comparer.
- **Time-stamps are `DateTimeOffset`.** Always serialize and round-trip in
  UTC; the `.NET` `JsonSerializer` default uses ISO-8601 with offset, which
  is fine, but mixing `DateTime` with `DateTimeOffset` in custom converters
  is a frequent source of off-by-one-hour bugs.

## Internals (for the curious)

These records compile down to immutable C# classes with synthesized
`Equals`, `GetHashCode`, and `Deconstruct`. Because every property is a
positional parameter, JSON serialisation via `System.Text.Json` round-trips
without any extra attributes — the property names match the parameter
names.

The choice to keep these as `record` types rather than mutable POCOs is
deliberate. DTOs that flow over the wire should be immutable by default;
mutation is reserved for the `Models` tier (which models EF Core entities).

## Dependencies

| Reference | Package / Project | Purpose                                      |
|-----------|-------------------|----------------------------------------------|
| —         | (none)            | This module is dependency-free.              |

## Java mapping

| .NET                        | Java                                     |
|-----------------------------|------------------------------------------|
| `CallbackConfigurationDto`  | `CallbackConfigurationDTO`               |
| `CallbackExecutionDto`      | `CallbackExecutionDTO`                   |
| `AuthorizedDomainDto`       | `AuthorizedDomainDTO`                    |
| `EventSubscriptionDto`      | `EventSubscriptionDTO`                   |
| `CallbackStatus`            | `CallbackStatus`                         |
| `CallbackExecutionStatus`   | `CallbackExecutionStatus`                |
| `CallbackHttpMethod`        | `CallbackHttpMethod`                     |

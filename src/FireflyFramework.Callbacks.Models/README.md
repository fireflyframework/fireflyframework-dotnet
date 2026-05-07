# FireflyFramework.Callbacks.Models

## Overview

`FireflyFramework.Callbacks.Models` carries the **persistence shapes** for
the outbound-callback subsystem. It defines the four entity classes a
relational store would expose, each annotated with whatever the Firefly
`BaseEntity<TId>` contract requires (a `Guid Id` plus the standard
audit-related companions). It sits between the pure-DTO `Interfaces` tier
and the runtime `Core` tier, and exists for one reason only: to let
applications persist callback configurations and audit logs *without* being
forced to reference Polly, ASP.NET Core, the dispatcher, the Polly retry
pipeline, the `CallbackRouter`, or the `IDomainAuthorizationService`.

This is the same pattern Firefly uses across every multi-project service
(see `FireflyFramework.RuleEngine.Models`, `FireflyFramework.Idp.Models`,
etc.). The Java equivalent is
`org.fireflyframework:firefly-callbacks-models`.

The module deliberately leaves out EF Core fluent configuration and
migrations — those are application concerns. What you get here is
framework-blessed POCOs with sensible defaults that you can wire into
`DbContext.OnModelCreating` however you like (column lengths, indexes,
JSON storage strategies all vary by database).

## When to use this module

- You want to persist `CallbackConfigurationDto` into your application's
  EF Core `DbContext`. Reference this module from the project that owns
  the `DbContext`; map the entities; provide a `DbSet<T>` for each.
- You want to write a custom `ICallbackConfigurationStore` (defined in
  `Callbacks.Core`) that talks to your real database — the entity shapes
  here are the natural target.
- You're consuming the framework's reference EF Core implementation
  (when one exists in your service) and need the entity types to register
  the `DbContext`.

You do **not** need this module if all you do is call the callback service
remotely through `Callbacks.Sdk` — the SDK only ever serialises the DTOs
in `Interfaces`.

## Mental model

The four entities map directly onto the four DTOs in `Interfaces`:

| DTO                          | Entity                          | Notes                                                      |
|------------------------------|---------------------------------|------------------------------------------------------------|
| `CallbackConfigurationDto`   | `CallbackConfigurationEntity`   | Lossy where DTO collections are stored as JSON columns     |
| `EventSubscriptionDto`       | `EventSubscriptionEntity`       | One row per `(configurationId, eventType)`                 |
| `AuthorizedDomainDto`        | `AuthorizedDomainEntity`        | Allow-list row; `AllowedIPsJson` is JSON-encoded           |
| `CallbackExecutionDto`       | `CallbackExecutionEntity`       | Per-dispatch audit row                                     |

The lossy fields (the JSON-string columns) are deliberate: relational
stores struggle with arrays-of-strings and dictionaries, so the entities
expose them as `string` properties suffixed with `Json`. The `Core` tier's
mapper is responsible for serialising and parsing those columns when it
hands DTOs back to controllers.

Every entity inherits `FireflyFramework.Data.Domain.BaseEntity<Guid>`,
which gives them the standard `Id` property plus whatever audit base
behaviour `BaseEntity<TId>` ships with in the data tier of the framework.

## Public surface

### `CallbackConfigurationEntity`

The persistence shape of a single configurable outbound callback. Default
values mirror what the dispatcher's Polly pipeline expects:

| Property                  | Type                | Default            | Notes                                                                |
|---------------------------|---------------------|--------------------|----------------------------------------------------------------------|
| `Name`                    | `string`            | `""`               | Required, set by the API on POST.                                    |
| `Url`                     | `string`            | `""`               | Validated against `IDomainAuthorizationService` at dispatch time.    |
| `HttpMethod`              | `CallbackHttpMethod`| `Post`             | One of `Post`, `Put`, `Patch`.                                       |
| `Status`                  | `CallbackStatus`    | `Active`           | Soft state machine: `Active`, `Paused`, `Disabled`, `Failed`.        |
| `SubscribedEventTypesJson`| `string`            | `"[]"`             | JSON-encoded `string[]`.                                             |
| `CustomHeadersJson`       | `string?`           | `null`             | JSON-encoded `Dictionary<string, string>`.                           |
| `Secret`                  | `string?`           | `null`             | HMAC-SHA256 signing key when `SignatureEnabled` is true.             |
| `SignatureEnabled`        | `bool`              | `false`            | If true, the dispatcher computes and adds the signature header.      |
| `SignatureHeader`         | `string?`           | `null`             | Defaults to `X-Signature` at dispatch time when null.                |
| `MaxRetries`              | `int`               | `3`                | Polly retry count.                                                   |
| `RetryDelayMs`            | `int`               | `1000`             | Initial Polly delay; exponential backoff with jitter.                |
| `RetryBackoffMultiplier`  | `double`            | `2.0`              | Multiplied per attempt by Polly.                                     |
| `TimeoutMs`               | `int`               | `30_000`           | Per-request timeout (separate from `MaxRetries`).                    |
| `Active`                  | `bool`              | `true`             | Hard switch; `false` short-circuits dispatch.                        |
| `TenantId`                | `string?`           | `null`             | Used by the router to scope event fan-out.                           |
| `FilterExpression`        | `string?`           | `null`             | Optional CEL/SpEL-like filter; format is application-defined.        |
| `MetadataJson`            | `string?`           | `null`             | JSON-encoded `Dictionary<string, object?>`.                          |
| `FailureThreshold`        | `int`               | `10`               | Auto-disables the callback once `FailureCount` exceeds this.         |
| `FailureCount`            | `int`               | `0`                | Reset to zero on the next successful dispatch.                       |
| `LastSuccessAt`           | `DateTimeOffset?`   | `null`             | Updated by the dispatcher.                                           |
| `LastFailureAt`           | `DateTimeOffset?`   | `null`             | Updated by the dispatcher.                                           |

### `EventSubscriptionEntity`

```csharp
public sealed class EventSubscriptionEntity : BaseEntity<Guid>
{
    public Guid    ConfigurationId { get; set; }
    public string  EventType       { get; set; } = string.Empty;
    public bool    IsActive        { get; set; } = true;
}
```

Recommended index: `(EventType, IsActive)` for fast fan-out lookup, with
`(ConfigurationId)` as a foreign-key index.

### `AuthorizedDomainEntity`

```csharp
public sealed class AuthorizedDomainEntity : BaseEntity<Guid>
{
    public string  Domain         { get; set; } = string.Empty;
    public string? AllowedIPsJson { get; set; }
    public bool    IsAuthorized   { get; set; } = true;
}
```

`Domain` is matched by suffix in `InMemoryDomainAuthorizationService`, so
storing `example.com` permits `partner.example.com`. The `AllowedIPsJson`
column is reserved for future per-domain IP-pinning; the in-memory
default does not consult it yet.

### `CallbackExecutionEntity`

```csharp
public sealed class CallbackExecutionEntity : BaseEntity<Guid>
{
    public Guid                          ConfigurationId    { get; set; }
    public string                        EventType          { get; set; } = string.Empty;
    public string                        SourceEventId      { get; set; } = string.Empty;
    public CallbackExecutionStatus       Status             { get; set; }
    public string?                       RequestPayload     { get; set; }
    public string?                       RequestHeaders     { get; set; }
    public int?                          ResponseStatusCode { get; set; }
    public string?                       ResponseBody       { get; set; }
    public int                           AttemptNumber      { get; set; }
    public int                           MaxAttempts        { get; set; }
    public long                          RequestDurationMs  { get; set; }
    public string?                       ErrorMessage       { get; set; }
    public DateTimeOffset                ExecutedAt         { get; set; }
    public DateTimeOffset?               CompletedAt       { get; set; }
}
```

Audit row written by `ICallbackExecutionStore.RecordAsync(...)` after every
dispatch. Most deployments partition this table by `ExecutedAt` and apply
a retention window of 30–90 days.

## Configuration

This module exposes no `IOptions<T>`. Mapping decisions (column
constraints, index strategy, JSON storage) belong in your application's
`DbContext.OnModelCreating`.

## Common patterns

### Wiring up a `DbContext`

```csharp
public sealed class CallbacksDbContext : DbContext
{
    public DbSet<CallbackConfigurationEntity> Configurations => Set<CallbackConfigurationEntity>();
    public DbSet<EventSubscriptionEntity>     Subscriptions  => Set<EventSubscriptionEntity>();
    public DbSet<AuthorizedDomainEntity>      AuthorizedDomains => Set<AuthorizedDomainEntity>();
    public DbSet<CallbackExecutionEntity>     Executions     => Set<CallbackExecutionEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CallbackConfigurationEntity>().HasIndex(x => x.TenantId);
        b.Entity<EventSubscriptionEntity>().HasIndex(x => new { x.EventType, x.IsActive });
        b.Entity<CallbackExecutionEntity>().HasIndex(x => x.ConfigurationId);
    }
}
```

### Round-tripping `SubscribedEventTypes`

```csharp
entity.SubscribedEventTypesJson = JsonSerializer.Serialize(dto.SubscribedEventTypes);
// ...
dto = dto with
{
    SubscribedEventTypes = JsonSerializer.Deserialize<string[]>(entity.SubscribedEventTypesJson) ?? []
};
```

## Pitfalls and gotchas

- **JSON columns vs typed columns**: PostgreSQL has native `jsonb`, SQL
  Server has `nvarchar(max) + json`. Both work; pick one and apply it
  consistently. The entity exposes the field as a plain `string`, which
  is the lowest common denominator.
- **`Status` vs `Active`**: do not collapse them. `Active = false` means
  "skip this row entirely"; `Status = Failed` means "the dispatcher
  auto-disabled this row but the operator can re-enable it".
- **Foreign keys are not declared on the entity**. The framework leaves
  it to your `DbContext` to express `EventSubscriptionEntity.ConfigurationId
  -> CallbackConfigurationEntity.Id`. The same applies to
  `CallbackExecutionEntity.ConfigurationId`.

## Internals (for the curious)

`BaseEntity<TId>` lives in `FireflyFramework.Data.Domain` and supplies the
`Id` property of type `TId`. We chose `Guid` rather than `long` because
configurations are created via REST and need a stable, non-sequential
identifier that can be embedded in a URL without leaking volume
information.

We deliberately store collections as JSON strings rather than as related
tables (apart from the `EventSubscriptionEntity` join), because the access
pattern is *always* "fetch the whole configuration as a single row". A
related table would require an extra join on every dispatch.

## Dependencies

| Reference                                | Used for                  |
|------------------------------------------|---------------------------|
| `FireflyFramework.Data`                  | `BaseEntity<TId>`         |
| `FireflyFramework.Callbacks.Interfaces`  | `CallbackHttpMethod`, `CallbackStatus`, `CallbackExecutionStatus` enums and DTO ↔ Entity mapping |

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `CallbackConfigurationEntity` | `CallbackConfiguration`             |
| `AuthorizedDomainEntity`      | `AuthorizedDomain`                  |
| `EventSubscriptionEntity`     | `EventSubscription`                 |
| `CallbackExecutionEntity`     | `CallbackExecution`                 |

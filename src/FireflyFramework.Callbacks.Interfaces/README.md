# FireflyFramework.Callbacks.Interfaces

Public DTOs and enums for the outbound-callback subsystem. Pure
contract module that REST clients can reference without pulling the
dispatch implementation.

Mirrors `org.fireflyframework:firefly-callbacks-interfaces`.

## Public surface

| Type                          | Purpose                                                                |
|-------------------------------|------------------------------------------------------------------------|
| `CallbackStatus`              | `Active`, `Paused`, `Disabled`, `Failed`                               |
| `CallbackExecutionStatus`     | `Success`, `FailedRetrying`, `FailedPermanent`                         |
| `CallbackHttpMethod`          | `Post`, `Put`, `Patch`                                                 |
| `CallbackConfigurationDto`    | Persistable callback configuration: name, URL, method, secret, signature options, retry policy, timeout, tenant, filter expression, metadata, failure threshold, audit timestamps |
| `CallbackExecutionDto`        | Per-dispatch audit row: id, configuration id, event type, source event id, status, request / response payload + headers, status code, attempt number, request duration, error message, executed / completed timestamps |
| `AuthorizedDomainDto`         | URL allow-list entry: `Domain`, `AllowedIPs`, `IsAuthorized`           |
| `EventSubscriptionDto`        | Maps `(ConfigurationId, EventType, IsActive)`                          |

## Dependencies

None — pure DTOs.

## Java mapping

| .NET                          | Java                                  |
|-------------------------------|---------------------------------------|
| `CallbackConfigurationDto`    | `CallbackConfigurationDTO`            |
| `CallbackExecutionDto`        | `CallbackExecutionDTO`                |
| `AuthorizedDomainDto`         | `AuthorizedDomainDTO`                 |
| `EventSubscriptionDto`        | `EventSubscriptionDTO`                |

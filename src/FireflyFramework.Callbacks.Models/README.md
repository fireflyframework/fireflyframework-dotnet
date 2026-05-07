# FireflyFramework.Callbacks.Models

EF Core persistence model for the callbacks subsystem. Sits between
`Interfaces` (DTOs) and `Core` (services) so application code can
persist configurations and audit logs without pulling the dispatch
runtime.

Mirrors `org.fireflyframework:firefly-callbacks-models`.

## Public surface

| Entity                          | Maps to                                                              |
|---------------------------------|----------------------------------------------------------------------|
| `CallbackConfigurationEntity`   | `firefly_callback_configurations` — name, URL, HTTP method, status, secret, signature options, retry / backoff / timeout, failure counters, tenant id, filter expression, metadata JSON |
| `AuthorizedDomainEntity`        | `firefly_callback_authorized_domains` — domain, allowed IPs JSON, IsAuthorized |
| `EventSubscriptionEntity`       | `firefly_callback_subscriptions` — configuration id, event type, active flag |
| `CallbackExecutionEntity`       | `firefly_callback_executions` — full audit row of a single dispatch  |

Every entity inherits `FireflyFramework.Data.BaseEntity<Guid>` for the
shared `Id` contract.

## Dependencies

| Reference                                | Used for                |
|------------------------------------------|-------------------------|
| `FireflyFramework.Data`                  | `BaseEntity<TId>`       |
| `FireflyFramework.Callbacks.Interfaces`  | DTO ↔ Entity mapping    |

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `CallbackConfigurationEntity` | `CallbackConfiguration`             |
| `AuthorizedDomainEntity`      | `AuthorizedDomain`                  |
| `EventSubscriptionEntity`     | `EventSubscription`                 |
| `CallbackExecutionEntity`     | `CallbackExecution`                 |

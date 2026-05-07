# FireflyFramework.Tests

xUnit test suite covering every tier of the framework. The whole suite
runs against the in-memory implementations of every adapter so it
finishes in a couple of seconds and needs no external infrastructure.

## Running

```bash
dotnet test tests/FireflyFramework.Tests/FireflyFramework.Tests.csproj
```

Expect:

```
Passed!  -  Failed: 0, Passed: 157, Skipped: 0, Total: 157
```

## File map

| Area                     | Files                                                                            |
|--------------------------|----------------------------------------------------------------------------------|
| Foundational             | `KernelTests`, `UtilsTemplateTests`, `ValidatorTests`, `WebMiddlewareTests`      |
| Cache / Observability    | `CacheTests`, `ObservabilityTests`                                               |
| Data                     | `DataFilterTests`                                                                |
| CQRS                     | `CqrsTests`                                                                      |
| EDA                      | `EdaTests`, `SerializerTests`                                                    |
| Event sourcing           | `EventSourcingTests`, `EfCoreEventStoreTests`, `EventSourcingExtraTests`         |
| Orchestration            | `SagaTests`, `WorkflowTests`, `TccTests`                                         |
| Rule engine              | `RuleEngineTests`, `YamlDslParserTests`                                          |
| Plugins                  | `PluginsTests`, `AssemblyPluginLoaderTests`                                      |
| Notifications            | `NotificationsTests`, `NotificationDispatcherTests`                              |
| IDP                      | `InternalDbIdpTests`                                                             |
| Client                   | `ClientTests`, `ClientTransportTests`                                            |
| Webhooks                 | `WebhookSignatureValidatorTests`, `WebhookServiceTests`                          |
| Back-office / Config     | `BackofficeTests`, `ConfigServerTests`                                           |
| Audit additions          | `AuditExtensionsTests`, `StubFixesTests`                                         |

## Test packages

`Directory.Build.targets` automatically references the following
NuGet packages for any project with `IsTestProject=true`, so this
csproj does not list them explicitly:

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `FluentAssertions`

This project additionally pulls `Microsoft.EntityFrameworkCore.InMemory`
and `Microsoft.AspNetCore.TestHost` for the EF Core event-store and
ConfigServer test fixtures.

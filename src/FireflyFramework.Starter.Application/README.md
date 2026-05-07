# FireflyFramework.Starter.Application

Application-tier starter. Layers the plugin extension registry and
plugin manager on top of `Starter.Core`. IDP / orchestration adapters
remain service-specific because each application picks one provider —
register them in your composition root.

Mirrors `org.fireflyframework:firefly-starter-application`.

## Usage

```csharp
using FireflyFramework.Starter.Application;

builder.Services.AddFireflyApplication(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

// Pick one IDP adapter:
builder.Services.AddSingleton<IIdpAdapter, KeycloakIdpAdapter>();
```

## What it adds on top of `AddFireflyCore`

- `IExtensionRegistry` — singleton, default `DefaultExtensionRegistry`
- `IPluginManager`     — singleton, default `DefaultPluginManager`

Plugins can then be loaded via `AssemblyPluginLoader` from
`FireflyFramework.Plugins.Core`.

## Dependencies

| Reference                                | Pulled in transitively  |
|------------------------------------------|-------------------------|
| `FireflyFramework.Starter.Core`          | always                  |
| `FireflyFramework.Plugins.Core`          | always                  |
| `FireflyFramework.Orchestration`         | always                  |
| `FireflyFramework.Idp`                   | always                  |

## Java mapping

| .NET                            | Java                                     |
|---------------------------------|------------------------------------------|
| `AddFireflyApplication`         | `fireflyframework-starter-application`   |

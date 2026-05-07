# FireflyFramework.Starter.Application

## Overview

`FireflyFramework.Starter.Application` is the **service-application-tier
meta-package** of the Firefly Framework for .NET. It is the right starter
for the majority of business microservices: anything that runs commands
and queries, talks to other services, and may need to load extension
plugins at runtime — but does not host event-sourced aggregates or own
its own database schema.

The starter sits one rung above `Starter.Core`. Calling
`services.AddFireflyApplication(...)` first invokes `AddFireflyCore` to
wire the seven infrastructure primitives (web, observability, cache, EDA,
CQRS, client, validators), then layers on the **plugin runtime**: an
`IExtensionRegistry` for declaring extension points and an
`IPluginManager` for the plugin lifecycle (load → initialize → start →
stop → unload). The service's composition root is then free to load
plugins from disk via `AssemblyPluginLoader`, register IDP / orchestration
adapters, and start handling requests.

The Java equivalent is `org.fireflyframework:firefly-starter-application`,
which composes `firefly-starter-core` with `firefly-plugin-core`, the
Keycloak / Cognito IDP starters, and the orchestration starter via Spring
Boot auto-configuration. The .NET version keeps the same composition but
makes the IDP / orchestration choice an **explicit** registration in the
consumer's `Program.cs`. Each service tends to pick exactly one IDP and
exactly one orchestration backend, so leaving that decision to the consumer
avoids accidentally pulling three IDP adapters into a single process.

## When to use this module

Reach for `Starter.Application` when:

- You are building a **standard business microservice** — the kind that
  exposes REST endpoints, dispatches CQRS commands, talks to other
  services via SDKs, and authenticates against an OIDC provider.
- The service may host **runtime plugins**: pricing rules, validation
  hooks, third-party adapters that ship as separate assemblies and load
  on demand.
- You want a back-office context resolver but do **not** need the
  `BackofficeMiddleware` — that is what `FireflyFramework.BackOffice`
  layers on top of this starter.

Prefer a different starter when:

- The service hosts event-sourced aggregates → `Starter.Domain`.
- The service is purely data-intensive (ETL, reporting) and you want a
  Polly-rich resilience surface plus your own `DbContext` →
  `Starter.Data`.
- The service is a back-office portal that performs customer impersonation
  → `FireflyFramework.BackOffice`.
- The service is a stateless worker / job runner with no plugins →
  `Starter.Core` is enough.

## Mental model

The starter is a **composition** of two layers:

```
                  ┌─────────────────────────────────────┐
                  │  AddFireflyApplication(...)         │
                  │  ┌───────────────────────────────┐  │
                  │  │  AddFireflyCore(...)          │  │
                  │  │  Web, Observability, Cache,   │  │
                  │  │  EDA, CQRS, Client, Valid.    │  │
                  │  └───────────────────────────────┘  │
                  │  + IExtensionRegistry               │
                  │  + IPluginManager                   │
                  └─────────────────────────────────────┘
```

What the consumer is still expected to register:

| Concern                | Pick exactly one and register it yourself                                  |
|------------------------|----------------------------------------------------------------------------|
| Identity provider      | `KeycloakIdpAdapter`, `AzureAdIdpAdapter`, `CognitoIdpAdapter`, `InternalDbIdpAdapter` |
| Orchestration runtime  | `FireflyFramework.Orchestration` workflow registry (only when needed)      |
| Domain plugins         | `services.AddSingleton<IPlugin, MyPlugin>()` or load via `AssemblyPluginLoader` |
| Cross-service clients  | `services.AddOrdersServiceClient(uri)`, `services.AddCallbackClient(uri)`, etc. |

The plugin runtime is **opt-in**. If the service has no plugins, simply
do not load any — `IPluginManager` is registered but its `All()` returns
empty, and there is zero runtime cost.

## Quick start

```csharp
using FireflyFramework.Starter.Application;
using FireflyFramework.Web.DependencyInjection;
using FireflyFramework.Idp;
using FireflyFramework.Idp.Keycloak;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFireflyApplication(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

// Pick one IDP — required by the application tier
builder.Services.AddSingleton<IIdpAdapter, KeycloakIdpAdapter>();

var app = builder.Build();
app.UseFireflyWeb();
app.MapControllers();
await app.RunAsync();
```

## Public surface

```csharp
namespace FireflyFramework.Starter.Application;

public static class FireflyApplicationExtensions
{
    public static IServiceCollection AddFireflyApplication(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies);
}
```

| Parameter        | Required | Purpose                                                                  |
|------------------|----------|--------------------------------------------------------------------------|
| `services`       | yes      | The DI container being configured.                                       |
| `config`         | yes      | The `IConfiguration` from which `Firefly:*` sections are bound.          |
| `serviceName`    | yes      | The OpenTelemetry `service.name` resource attribute. Used in the banner. |
| `serviceVersion` | no       | The OpenTelemetry `service.version` attribute. Defaults to `"1.0.0"`.    |
| `cqrsAssemblies` | no       | Assemblies scanned for `ICommandHandler<,>` and `IQueryHandler<,>`.      |

After the call, two extra contracts are resolvable from DI:

| Service              | Default implementation       | Lifetime  | Source                                |
|----------------------|------------------------------|-----------|---------------------------------------|
| `IExtensionRegistry` | `DefaultExtensionRegistry`   | Singleton | `FireflyFramework.Plugins.Core`       |
| `IPluginManager`     | `DefaultPluginManager`       | Singleton | `FireflyFramework.Plugins.Core`       |

Both are registered with `TryAddSingleton`, so a service that wants its
own `IPluginManager` simply registers it before calling
`AddFireflyApplication`.

## Configuration

`Starter.Application` adds no new configuration sections of its own. All
of the bound sections are inherited from `Starter.Core` — see
[Starter.Core/README.md](../FireflyFramework.Starter.Core/README.md) for
the complete list.

The plugin runtime is configured imperatively rather than via JSON: load
plugins at startup using the `AssemblyPluginLoader`, then start them by
plugin id.

## Common patterns

### 1. Loading plugins from a directory

```csharp
public sealed class PluginStartupService(
    AssemblyPluginLoader loader,
    IPluginManager manager,
    IConfiguration config) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var dir = config["Firefly:Plugins:Directory"] ?? "./plugins";
        if (!Directory.Exists(dir)) return;

        foreach (var dll in Directory.EnumerateFiles(dir, "*.dll"))
        {
            var descriptors = await loader.LoadFromAssemblyAsync(dll, ct);
            foreach (var d in descriptors)
            {
                await manager.StartAsync(d.Id, ct);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

builder.Services.AddSingleton<AssemblyPluginLoader>();
builder.Services.AddHostedService<PluginStartupService>();
```

### 2. Registering an extension point and plugins as extensions

```csharp
// Define an extension contract somewhere in your domain
public interface IPricingRule
{
    decimal Apply(decimal subtotal);
}

// At startup
var registry = app.Services.GetRequiredService<IExtensionRegistry>();
registry.RegisterExtensionPoint("orders.pricing", typeof(IPricingRule));

// In a plugin's IPlugin.InitializeAsync
public Task InitializeAsync(CancellationToken ct)
{
    // _registry was passed in via DI
    _registry.RegisterExtension<IPricingRule>("orders.pricing", new BlackFridayRule(), priority: 100);
    return Task.CompletedTask;
}

// In a command handler
var rules = registry.GetExtensions<IPricingRule>("orders.pricing");
foreach (var rule in rules) subtotal = rule.Apply(subtotal);
```

### 3. Picking and wiring an IDP adapter

```csharp
// Keycloak
builder.Services.AddSingleton<IIdpAdapter, KeycloakIdpAdapter>();

// Azure AD
builder.Services.AddSingleton<IIdpAdapter, AzureAdIdpAdapter>();

// AWS Cognito
builder.Services.AddSingleton<IIdpAdapter, CognitoIdpAdapter>();

// Internal database (development / on-prem)
builder.Services.AddSingleton<IIdpAdapter, InternalDbIdpAdapter>();
```

The starter does not pick one for you because each adapter has its own
configuration block (`Firefly:Idp:Keycloak`, `Firefly:Idp:AzureAd`, etc.)
and you only ever want one active per process.

### 4. Adding orchestration when the service runs sagas

```csharp
builder.Services.AddSingleton<IWorkflowRegistry, DefaultWorkflowRegistry>();
builder.Services.AddSingleton<IWorkflowEngine, DefaultWorkflowEngine>();
```

The orchestration project is referenced transitively by this starter, so
no extra package reference is required.

### 5. Composing with cross-service SDKs

```csharp
builder.Services.AddOrdersServiceClient(new Uri("https://orders.svc.cluster.local"));
builder.Services.AddCallbackClient(new Uri("https://callbacks.svc.cluster.local"));
```

Each SDK extension uses `AddHttpClient<TInterface, TImplementation>`, so
the resulting `HttpClient` participates in the standard
`IHttpClientFactory` pool. Service discovery is provided transitively by
`Microsoft.Extensions.ServiceDiscovery`.

## Pitfalls and gotchas

- **Do not call `AddFireflyCore` and `AddFireflyApplication` together.**
  The application starter calls core internally; calling it twice
  appends duplicate exception converters and prints the banner more than
  once. Pick exactly one starter.
- **Plugins live in their own AssemblyLoadContext.** `AssemblyPluginLoader`
  uses `McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(...,
  isUnloadable: true)`. Sharing types across the boundary requires that
  the type is in the `sharedTypes` list (the loader hard-codes `IPlugin`
  and `IExtensionRegistry`); other types are reloaded into the plugin's
  context and will not pass `is`/`as` checks against the host's instance.
- **`IPluginManager` is a singleton.** Plugin handlers therefore live
  for the application's lifetime. Per-request state belongs in DI scopes,
  not in the plugin itself.
- **Plugin metadata must be unique.** `DefaultPluginManager` keys
  plugins by `metadata.Id`; loading two plugins with the same id silently
  replaces the first one in the dictionary.
- **The IDP is intentionally not registered.** A failure to register one
  manifests as `InvalidOperationException: No service for type 'IIdpAdapter'
  has been registered` on the first request that resolves it. Register
  one in `Program.cs`.
- **`IExtensionRegistry` is mutable at runtime.** Plugins can both add
  and remove extensions while the application is running, so do not
  cache `GetExtensions` lists between calls.

## Internals (for the curious)

`AddFireflyApplication` is four lines:

```csharp
public static IServiceCollection AddFireflyApplication(...)
{
    FireflyBanner.Print(typeof(FireflyApplicationExtensions).Assembly, serviceName, serviceVersion);
    services.AddFireflyCore(config, serviceName, serviceVersion, cqrsAssemblies);
    services.TryAddSingleton<IExtensionRegistry, DefaultExtensionRegistry>();
    services.TryAddSingleton<IPluginManager, DefaultPluginManager>();
    return services;
}
```

The first banner call happens against the `Starter.Application` assembly,
which embeds its own `Resources/banner.txt` — the tagline reads
`:: firefly-application ::`. Because `FireflyBanner._printed` is a
process-wide latch, the second call inside `AddFireflyCore` is a no-op,
so the consumer sees exactly one application banner.

`DefaultPluginManager` keeps loaded plugins in a
`ConcurrentDictionary<string, (IPlugin, PluginDescriptor)>`. Lifecycle
transitions update the descriptor's `State` (Loaded → Initialized →
Started → Stopped → Destroyed) via `descriptor.WithState(...)`, then
write the new tuple back. Each transition asynchronously dispatches to
the plugin's own `InitializeAsync` / `StartAsync` / `StopAsync` /
`DestroyAsync`.

`DefaultExtensionRegistry` keeps two dictionaries: one mapping point id
→ contract type, the other mapping point id → list of (extension,
priority). The list is sorted descending by priority on every insert,
so `GetExtensions` returns higher-priority extensions first.

## Dependencies

| Reference                                       | Why                                                              |
|-------------------------------------------------|------------------------------------------------------------------|
| `FireflyFramework.Starter.Core`                 | All of the infrastructure tier                                   |
| `FireflyFramework.Plugins.Core`                 | `DefaultExtensionRegistry`, `DefaultPluginManager`, plugin loader |
| `FireflyFramework.Orchestration`                | Saga / workflow primitives, available transitively               |
| `FireflyFramework.Idp`                          | `IIdpAdapter` contract and DTOs (you pick the implementation)    |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Bearer-token wiring for the IDP adapters                         |
| `Yarp.ReverseProxy`                             | Optional gateway / proxying scenarios                            |

The package also embeds `Resources/banner.txt` containing the
`firefly-application` ASCII tag printed at startup.

## Java mapping

| .NET                                | Java                                                       |
|-------------------------------------|------------------------------------------------------------|
| `AddFireflyApplication`             | `org.fireflyframework:firefly-starter-application`         |
| `IExtensionRegistry`                | `ExtensionRegistry`                                        |
| `IPluginManager`                    | `PluginManager`                                            |
| `DefaultPluginManager`              | `DefaultPluginManager`                                     |
| `AssemblyPluginLoader`              | `JarPluginLoader` (URLClassLoader-based)                   |
| `IPlugin`                           | `Plugin`                                                   |
| Resources/banner.txt                | `src/main/resources/banner.txt`                            |

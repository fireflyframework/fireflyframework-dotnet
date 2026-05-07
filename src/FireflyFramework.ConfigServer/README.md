# FireflyFramework.ConfigServer

## Overview

`FireflyFramework.ConfigServer` is a **standalone ASP.NET Core 10
host** that serves a configuration endpoint compatible with the
Spring Cloud Config "native" protocol. Java clients (via Spring Cloud
Config) and Steeltoe-based .NET clients can both consume it, so a
single config server can sit in front of services on either
platform.

It mirrors `org.fireflyframework:firefly-config-server`. The
endpoint paths, response envelope, and lookup precedence match Spring
Cloud Config exactly so the cross-platform story works out of the
box.

## Why a separate executable host?

Most Firefly modules are libraries — referenced from a service and
composed into the host application. The config server is different:
it's an *application* in its own right that other services consume.
Packaging it as a runnable host:

- Lets you deploy it as a sidecar or shared infrastructure service.
- Keeps configuration management out of every service's process.
- Lets Java services on the same platform consume it without
  modification (Spring Cloud Config's "native" mode is what they're
  already wired for).

This is the only project in the framework with `<IsPackable>false</IsPackable>`
— it's an executable, not a library.

## Mental model

```
   filesystem with config files
        │
        │  ./config/orders-prod.yml
        │  ./config/orders.yml
        │  ./config/application-prod.yml
        │  ./config/application.yml
        ▼
   ┌──────────────────────────────────┐
   │  FireflyFramework.ConfigServer   │
   │  (this host)                     │
   │   - HTTP endpoints below         │
   │   - YAML / JSON / properties     │
   │     parsers                      │
   └──────────┬───────────────────────┘
              │  Spring-Cloud-Config envelope
              │
   ┌──────────┴───────────────────────┐
   │ consumer services                │
   │   • Java (Spring Cloud Config)   │
   │   • .NET (Steeltoe Config)       │
   └──────────────────────────────────┘
```

The server is read-only — operators edit files on disk (or mount
them from a Git repository), the server reads, parses, and serves.

## Endpoints

| Method | Path                                  | Description                                         |
|--------|---------------------------------------|-----------------------------------------------------|
| GET    | `/{application}/{profile}`            | Returns a Spring-Cloud-Config envelope for the supplied app and comma-separated profiles |
| GET    | `/{application}/{profile}/{label}`    | As above, with a label                              |
| GET    | `/health`                             | Liveness probe                                      |

The envelope shape matches Spring's exactly:

```json
{
  "name":           "orders",
  "profiles":       [ "prod" ],
  "label":          null,
  "version":        null,
  "state":          null,
  "propertySources": [
    {
      "name":   "file:./config/orders-prod.yml",
      "source": { "spring.profile": "prod", "foo": "bar" }
    }
  ]
}
```

`propertySources` is an *ordered* array — earlier entries override
later ones. The server emits sources in lookup-precedence order
(see below).

## Lookup precedence

For each request, the first matching file (from top to bottom) is
returned, scanning every supported extension in order:

1. `{application}-{profile}.{ext}`
2. `{application}.{ext}`
3. `application-{profile}.{ext}`
4. `application.{ext}`

`{ext}` ∈ { `yml`, `yaml`, `json`, `properties` }.

This precedence matches Spring Cloud Config's: app+profile most
specific, then app-only, then default+profile, then default. Within
each precedence level, YAML wins over JSON wins over properties.

## Configuration

```json
{
  "Firefly": {
    "ConfigServer": {
      "SearchDirectory": "./config"
    }
  }
}
```

`SearchDirectory` defaults to `./config` relative to the host's
working directory. Most deployments mount a Kubernetes ConfigMap or
a Git checkout there.

## Wiring (in your own host)

```csharp
using FireflyFramework.ConfigServer;

builder.Services.AddRouting();
builder.Services.AddFireflyConfigServer(builder.Configuration);

var app = builder.Build();
app.UseRouting();
app.UseEndpoints(e => e.MapFireflyConfigServer());
await app.RunAsync();
```

The repository's `Program.cs` does exactly this and runs as a
standalone service on its own.

## Common patterns

### Mounting a Kubernetes ConfigMap

```yaml
# deployment.yaml
volumes:
  - name: firefly-config
    configMap:
      name: firefly-app-config
volumeMounts:
  - name: firefly-config
    mountPath: /app/config
    readOnly: true
```

```yaml
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: firefly-app-config
data:
  orders-prod.yml: |
    spring:
      datasource:
        url: jdbc:postgresql://orders-db:5432/orders
    server:
      port: 8080
```

The pod's filesystem at `/app/config/orders-prod.yml` is what the
config server reads.

### Consuming from a .NET service via Steeltoe

```csharp
builder.Configuration.AddConfigServer(builder.Configuration);
```

With `spring:application:name = orders` and
`spring:cloud:config:uri = http://config-server:8888` set, Steeltoe
calls the server's `/orders/{profile}` endpoint and merges the
returned property sources into `IConfiguration`.

### Consuming from a Java service via Spring Cloud Config

```yaml
spring:
  application:
    name: orders
  cloud:
    config:
      uri: http://config-server:8888
      profile: prod
```

Spring Cloud Config calls the same endpoint and gets the same
envelope. No special handling needed.

### Hot-reload via filesystem watch

The config server itself doesn't watch the filesystem — Spring Cloud
Config's `/actuator/refresh` endpoint isn't implemented. Operators
restart consumer services after a config change, or use the
consumer-side refresh affordance (`@RefreshScope` in Spring,
`IOptionsMonitor<T>` in .NET).

## Pitfalls and gotchas

- **Property file format is Java-style.** `key=value`, `\` for line
  continuation, `\n` for newline. Don't use `.NET`'s
  `appsettings.json` format and expect it to work — use JSON, YAML,
  or `.properties` explicitly.
- **YAML is parsed strictly.** Tabs are illegal (per YAML spec); use
  spaces. Inline objects are supported but sparingly used in
  practice.
- **Profile order matters in the URL.** `/orders/dev,prod` is *not*
  the same as `/orders/prod,dev`. Later profiles in the list override
  earlier ones in Spring's precedence.
- **`label` is currently ignored.** The native protocol doesn't use
  it; it's accepted for compatibility but the server's filesystem
  layout has no label dimension. For Git-backed config, fork the
  server or use Spring Cloud Config Server (this is a "native"
  implementation).
- **No authentication out of the box.** Add an auth middleware in
  the host pipeline. Configuration is sensitive — a misconfigured
  server can leak secrets.
- **`/health` is unconditional 200.** It checks process liveness,
  not config readability. For deeper health, fork or layer your own.

## Internals (for the curious)

- The host's `Program.cs` is intentionally tiny — `Web` ASP.NET
  with routing only, no controllers, no MVC.
- Property source parsing is in-process: YamlDotNet for `.yml`/
  `.yaml`, `System.Text.Json` for `.json`, custom parser for
  `.properties`. No external runtime dependencies on the consumer's
  classpath.
- The endpoints are `app.MapGet(...)` minimal API endpoints rather
  than controllers. There's no routing template magic — the
  three-segment vs two-segment path detection is straightforward
  pattern matching.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `Microsoft.AspNetCore.App` (FrameworkRef)| Hosting, routing               |
| `Steeltoe.Configuration.ConfigServer`    | Pulled by tests; not required at runtime |

## Java mapping

| .NET                            | Java                                       |
|---------------------------------|--------------------------------------------|
| `MapFireflyConfigServer`        | `@EnableConfigServer` (Spring Cloud Config)|
| `AddFireflyConfigServer`        | Spring Cloud Config Server auto-config     |
| `Program.cs`                    | `FireflyConfigServerApplication`           |

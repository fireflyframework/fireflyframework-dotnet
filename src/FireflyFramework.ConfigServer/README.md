# FireflyFramework.ConfigServer

Standalone ASP.NET Core 9 host that serves a configuration endpoint
compatible with the Spring Cloud Config "native" protocol. Java clients
and Steeltoe-based .NET clients can both consume it, so a single config
server can sit in front of services on either platform.

Mirrors `org.fireflyframework:firefly-config-server`.

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

## Lookup precedence

For each request, the first matching file (from top to bottom) is
returned, scanning every supported extension in order:

1. `{application}-{profile}.{ext}`
2. `{application}.{ext}`
3. `application-{profile}.{ext}`
4. `application.{ext}`

`{ext}` ∈ { `yml`, `yaml`, `json`, `properties` }.

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

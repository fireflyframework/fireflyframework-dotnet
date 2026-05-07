# FireflyFramework.Actuator

Spring Boot Actuator port. Exposes `/actuator/*` JSON endpoints for
runtime introspection and integrates with the existing Firefly health
checks (`FireflyFramework.Observability`).

## Built-in endpoints

| Endpoint | Source | Notes |
|---|---|---|
| `/actuator/info` | `InfoEndpoint` | application name, version, runtime |
| `/actuator/env` | `EnvEndpoint` | configuration sources with secret masking |
| `/actuator/beans` | `BeansEndpoint` | DI registration introspection |
| `/actuator/metrics` | `MetricsEndpoint` | process + GC + uptime counters |
| `/actuator/loggers` | `LoggersEndpoint` | available log levels |
| `/actuator/threaddump` | `ThreadDumpEndpoint` | per-thread state and CPU time |
| `/actuator/mappings` | `MappingsEndpoint` | route table dump |

The standard `/health` endpoint stays in `FireflyFramework.Observability`
(it predates this module). The actuator router adds `/actuator/health`
that simply reuses the same `HealthCheckService`.

## Quick start

```csharp
services.AddFireflyActuator(Configuration);

var app = builder.Build();
app.MapFireflyActuator();
```

```yaml
Firefly:
  Actuator:
    BasePath: /actuator
    ExposeEndpoints: [info, metrics, env, beans, loggers]
    RequireAuthorization: true
```

Custom endpoints implement `IActuatorEndpoint` and register via
`services.AddActuatorEndpoint<MyEndpoint>()`.

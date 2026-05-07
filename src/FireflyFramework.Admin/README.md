# FireflyFramework.Admin

Spring Boot Admin Server port. Two halves:

* **Server** — hosts an `IInstanceRegistry` and exposes a small JSON API
  (`POST /admin/instances`, `PUT /admin/instances/{id}/heartbeat`,
  `GET /admin/instances`).
* **Client** — runs as a `BackgroundService` inside each registered
  application, posts a registration once and a heartbeat every
  `HeartbeatInterval`.

## Server

```csharp
services.AddFireflyAdminServer(Configuration);
app.MapFireflyAdminServer();
```

```yaml
Firefly:
  Admin:
    Server:
      BasePath: /admin
      HeartbeatTimeout: 00:01:00
```

## Client

```csharp
services.AddFireflyAdminClient(Configuration);
```

```yaml
Firefly:
  Admin:
    Client:
      ServerUrl: http://admin:8080/admin
      Name: orders-api
      ManagementUrl: http://orders-api:8080/actuator
      HealthUrl: http://orders-api:8080/health
      HeartbeatInterval: 00:00:15
      AutoRegister: true
      Metadata:
        team: payments
```

# FireflyFramework.Callbacks.Web

ASP.NET Core controllers exposing the callback-configuration store over
HTTP. Mirrors `org.fireflyframework:firefly-callbacks-web`.

## Endpoints

| Method | Path                                       | Body                       | Description                       |
|--------|--------------------------------------------|----------------------------|-----------------------------------|
| GET    | `/api/callbacks/configurations`            | `?tenantId=` (optional)    | List configurations               |
| GET    | `/api/callbacks/configurations/{id:guid}`  | —                          | Get one configuration             |
| POST   | `/api/callbacks/configurations`            | `CallbackConfigurationDto` | Create a configuration            |
| PUT    | `/api/callbacks/configurations/{id:guid}`  | `CallbackConfigurationDto` | Update an existing configuration  |
| DELETE | `/api/callbacks/configurations/{id:guid}`  | —                          | Delete a configuration            |

The controller delegates to `ICallbackConfigurationStore` from
`Callbacks.Core`. Replace the in-memory default with an EF Core
implementation by registering your own `ICallbackConfigurationStore`
before this controller is added.

## Wiring

```csharp
using FireflyFramework.Callbacks.Core;
using FireflyFramework.Callbacks.Web;

builder.Services.AddSingleton<ICallbackConfigurationStore, InMemoryCallbackConfigurationStore>();
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(CallbackConfigurationController).Assembly);
```

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.Callbacks.Core`        | `ICallbackConfigurationStore`       |
| `FireflyFramework.Callbacks.Interfaces`  | DTOs                                |
| `Microsoft.AspNetCore.App`               | `[ApiController]`, MVC binding      |

## Java mapping

| .NET                                  | Java                                |
|---------------------------------------|-------------------------------------|
| `CallbackConfigurationController`     | `CallbackConfigurationController`   |

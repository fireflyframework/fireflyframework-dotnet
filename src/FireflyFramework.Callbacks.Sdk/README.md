# FireflyFramework.Callbacks.Sdk

Typed `HttpClient` wrapper for the callback-management REST API exposed
by `FireflyFramework.Callbacks.Web`. Use it from any .NET service that
needs to register or list callbacks remotely.

Mirrors `org.fireflyframework:firefly-callbacks-sdk`.

## Usage

```csharp
using FireflyFramework.Callbacks.Sdk;

builder.Services
    .AddHttpClient<CallbackClient>(c => c.BaseAddress = new Uri("https://callbacks.svc.local"));

var configurations = await client.ListAsync(ct);
var created        = await client.CreateAsync(new CallbackConfigurationDto(/* ... */), ct);
```

## Public surface

| Method          | Calls                                                  |
|-----------------|--------------------------------------------------------|
| `ListAsync`     | `GET  /api/callbacks/configurations`                   |
| `CreateAsync`   | `POST /api/callbacks/configurations`                   |

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Callbacks.Interfaces`  | DTOs                           |
| `System.Net.Http.Json`                   | Typed JSON HTTP                |

## Java mapping

| .NET              | Java                              |
|-------------------|-----------------------------------|
| `CallbackClient`  | `CallbackClient`                  |

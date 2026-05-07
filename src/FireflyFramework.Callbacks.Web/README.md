# FireflyFramework.Callbacks.Web

## Overview

`FireflyFramework.Callbacks.Web` is the **HTTP face** of the outbound-callback
subsystem. It exposes a single ASP.NET Core controller, `CallbackConfigurationController`,
that lets remote clients (typically operators using the Firefly admin UI or
CI/CD tooling) create, list, read, update, and delete callback configurations
through a conventional REST API.

This is a deliberately thin layer. The controller does not host any business
logic — it delegates straight to `ICallbackConfigurationStore` from
`Callbacks.Core`. That separation means the same store implementation is
exercised whether you're consuming the framework in-process from a saga
or remotely from a web client; only the *wire format* changes.

The mental model is "REST CRUD over `CallbackConfigurationDto`". The
controller does not expose execution audit logs, the dispatcher itself, the
domain authorization allow-list, or the event subscription map — those
endpoints can be added by your application if you need them, but the
framework only opinionates on configuration management.

This module mirrors `org.fireflyframework:firefly-callbacks-web` from the
Java line.

## When to use this module

- You are building a Firefly **callback service** — i.e. a microservice whose
  job is to manage callback configurations and route domain events. Reference
  this module from the web project so that the controllers ship with your
  binary.
- You want a standardised admin REST surface so that other Firefly services
  can manage their own callback subscriptions remotely (the typed
  `Callbacks.Sdk` consumes exactly this surface).
- You're writing integration tests that need a real HTTP server to exercise
  the controller; reference this module from your test host.

You do **not** need this module if all you want is in-process dispatch from
a saga — that case only needs `Callbacks.Core`.

## Mental model

```
┌──────────────────────────────────────────┐
│  CallbackConfigurationController         │
│   (this module — Web)                    │
│                                          │
│   GET    /api/callbacks/configurations   │──┐
│   GET    /api/callbacks/configurations/{id}│ │
│   POST   /api/callbacks/configurations   │ │ delegates
│   PUT    /api/callbacks/configurations/{id}│ │ to
│   DELETE /api/callbacks/configurations/{id}│ │
└──────────────────────────────────────────┘ │
                                              ▼
                          ┌──────────────────────────────────┐
                          │  ICallbackConfigurationStore     │
                          │   (Callbacks.Core)               │
                          │                                  │
                          │   InMemoryCallbackConfiguration- │
                          │   Store is the default; replace  │
                          │   with EF Core in production.    │
                          └──────────────────────────────────┘
```

The controller has zero state of its own; everything is in the injected
store. That is the canonical Firefly pattern: every web tier is "controllers
that delegate to a service registered in DI", and the service is freely
swappable.

## Quick start

In your `Program.cs`:

```csharp
using FireflyFramework.Callbacks.Core;
using FireflyFramework.Callbacks.Web;

var builder = WebApplication.CreateBuilder(args);

// 1. Plug in the store. Use EF Core in production, in-memory otherwise.
builder.Services.AddSingleton<ICallbackConfigurationStore, InMemoryCallbackConfigurationStore>();

// 2. Add the controller. ApplicationPart picks up the assembly.
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(CallbackConfigurationController).Assembly);

var app = builder.Build();
app.MapControllers();
app.Run();
```

Then exercise it from any HTTP client:

```bash
curl -X POST https://callbacks.svc.local/api/callbacks/configurations \
  -H 'content-type: application/json' \
  -d '{
    "id": null,
    "name": "OrderEvents",
    "url": "https://partner.example.com/hooks/orders",
    "httpMethod": "Post",
    "status": "Active",
    "subscribedEventTypes": ["order.created"],
    ...
  }'
```

## Public surface

### `CallbackConfigurationController`

The single controller in this module. Routed at `api/callbacks/configurations`
with the `[ApiController]` attribute (which gives you automatic 400 responses
on model-binding failures, attribute routing-only, and inferred binding
sources).

| Method | Route                                       | Body                       | Response           |
|--------|---------------------------------------------|----------------------------|--------------------|
| GET    | `/api/callbacks/configurations`             | `?tenantId=` (optional)    | `200 IReadOnlyList<CallbackConfigurationDto>` |
| GET    | `/api/callbacks/configurations/{id:guid}`   | —                          | `200 CallbackConfigurationDto` or `404`        |
| POST   | `/api/callbacks/configurations`             | `CallbackConfigurationDto` | `201 CallbackConfigurationDto` (with `Location`) |
| PUT    | `/api/callbacks/configurations/{id:guid}`   | `CallbackConfigurationDto` | `200 CallbackConfigurationDto` or `404`        |
| DELETE | `/api/callbacks/configurations/{id:guid}`   | —                          | `204 No Content` or `404`                      |

The `:guid` route constraint is important: a non-GUID `id` returns 404 from
the routing layer rather than reaching the action method. POST always
assigns a new id (the store ignores any id supplied by the client).

### Action signatures

```csharp
[ApiController]
[Route("api/callbacks/configurations")]
public sealed class CallbackConfigurationController : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CallbackConfigurationDto>> List([FromQuery] string? tenantId = null, CancellationToken ct = default);

    [HttpGet("{id:guid}")]
    public Task<ActionResult<CallbackConfigurationDto>> Get(Guid id, CancellationToken ct);

    [HttpPost]
    public Task<ActionResult<CallbackConfigurationDto>> Create([FromBody] CallbackConfigurationDto dto, CancellationToken ct);

    [HttpPut("{id:guid}")]
    public Task<ActionResult<CallbackConfigurationDto>> Update(Guid id, [FromBody] CallbackConfigurationDto dto, CancellationToken ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct);
}
```

`Create` returns `CreatedAtAction(nameof(Get), new { id = stored.Id }, stored)`,
which sets a `Location: /api/callbacks/configurations/{id}` header on the
`201` response.

## Configuration

The controller has no `IOptions<T>`; all behaviour is delegated to the
injected `ICallbackConfigurationStore`. Configure store behaviour where the
store is registered.

### Authentication / authorization

The controller does not declare `[Authorize]`. If your service requires
authentication, add a global authorization policy via your endpoint
conventions or fall through `app.UseAuthentication()`/`app.UseAuthorization()`
before `app.MapControllers()`. A common pattern:

```csharp
builder.Services
    .AddControllers(options =>
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    })
    .AddApplicationPart(typeof(CallbackConfigurationController).Assembly);
```

## Common patterns

### Wiring the controller alongside an EF Core store

```csharp
builder.Services.AddDbContext<CallbacksDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Callbacks")));

builder.Services.AddScoped<ICallbackConfigurationStore, EfCoreCallbackConfigurationStore>();

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(CallbackConfigurationController).Assembly);
```

### Filtering by tenant

```bash
GET /api/callbacks/configurations?tenantId=alpha
```

The store's `ListAsync(tenantId)` overload handles the predicate; the
controller just forwards.

### Customising route prefix

The default route is `api/callbacks/configurations`. To change it without
modifying this assembly, layer your own controller on top that delegates,
or use ASP.NET Core's route convention API to rewrite the prefix at startup.

## Pitfalls and gotchas

- **`InMemoryCallbackConfigurationStore` is process-local.** Two instances
  of your service will diverge. Always replace it with a real store before
  running more than one replica.
- **The `id` in the body is ignored** on `POST`. The store always assigns a
  new `Guid`. On `PUT`, the path id wins; if the body contains a different
  id the store rewrites it.
- **No partial updates**. `PUT` replaces the entire row. The framework does
  not provide a `PATCH` endpoint; if you need merge semantics, project to
  RFC 7396 / 6902 in your application.
- **`Microsoft.AspNetCore.App` framework reference** — this module is only
  buildable in a project targeting `net9.0` (or whichever ASP.NET Core
  version the framework currently targets); it cannot be referenced from
  netstandard libraries.

## Internals (for the curious)

The controller is registered via `AddApplicationPart(typeof(CallbackConfigurationController).Assembly)`.
ASP.NET Core's `ApplicationPartManager` then discovers the controller by
type and adds it to the route table. The controller does *not* need to be
in the host's main assembly — that is the point of `ApplicationPart`.

Why CRUD-only? Because Firefly treats configuration storage as the
admin-plane concern of the service. The data plane (the actual dispatch)
goes through the saga / EDA layer in your application; you don't need an
HTTP endpoint for it. If you do, write your own controller that calls
`ICallbackRouter.RouteAsync` and adapts the response.

The choice to surface `IReadOnlyList<T>` rather than `IEnumerable<T>` from
the GET handlers is intentional: it materialises the list before the
serializer touches it, which avoids the gotcha where `IEnumerable<T>`
serializers consume the stream incrementally and surface partial results
on cancellation.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Callbacks.Core`        | `ICallbackConfigurationStore` and friends |
| `FireflyFramework.Web`                   | Shared MVC conventions         |
| `Microsoft.AspNetCore.App` (framework)   | `[ApiController]`, MVC binding |

## Java mapping

| .NET                                  | Java                                |
|---------------------------------------|-------------------------------------|
| `CallbackConfigurationController`     | `CallbackConfigurationController`   |
| `[ApiController]` + `[Route]`         | `@RestController` + `@RequestMapping` |
| `[FromBody]`                          | `@RequestBody`                      |
| `[FromQuery]`                         | `@RequestParam`                     |
| `CreatedAtAction`                     | `ResponseEntity.created(...)`       |

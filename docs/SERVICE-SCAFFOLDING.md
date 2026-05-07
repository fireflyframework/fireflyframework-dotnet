# Service Scaffolding

How to lay out a new microservice on top of FireflyFramework.NET.

The framework prescribes a single, opinionated project structure that
mirrors the multi-module Maven layout used by every Java service in the
Firefly platform (e.g. `core-banking-accounts`, `core-banking-cards`,
`core-banking-payments`). Adopting it gives you:

- A clear physical boundary between **contract**, **persistence**,
  **business logic**, **transport**, and **client SDK**.
- Strict dependency layering enforced by `csproj` references — the
  compiler refuses to let you accidentally call into the wrong layer.
- Trivial cross-service consumption — other services pull in your
  `.Sdk` and `.Interfaces` and nothing else.
- Symmetry with the Java side — Java services can be ported to .NET (or
  vice-versa) project-for-project.

---

## The five projects

| Suffix         | What lives here                                                                                | Project SDK              | Java analogue            |
|----------------|------------------------------------------------------------------------------------------------|--------------------------|--------------------------|
| `.Interfaces`  | Public DTOs, request/response records, enums. The wire contract.                               | `Microsoft.NET.Sdk`      | `*-interfaces` Maven module |
| `.Models`      | Persistence entities + repository contracts. The storage contract.                             | `Microsoft.NET.Sdk`      | `*-models` Maven module     |
| `.Core`        | Commands, queries, handlers, mappers, business services. The rules.                            | `Microsoft.NET.Sdk`      | `*-core` Maven module       |
| `.Web`         | Runnable ASP.NET Core 10 host: `Program.cs`, controllers / minimal-API endpoints, `appsettings`.| `Microsoft.NET.Sdk.Web`  | `*-web` (Spring Boot) module |
| `.Sdk`         | Typed `HttpClient` for in-process callers. References only `.Interfaces`.                      | `Microsoft.NET.Sdk`      | `*-sdk` Maven module        |

## The dependency graph

```
                     Interfaces
                    ╱     │     ╲
                  Sdk   Models   (no other ref)
                          │
                        Core
                          │
                         Web
```

- `Interfaces` is a **leaf**. It must not reference any other internal
  project — that is what makes it suitable for cross-service reuse.
- `Sdk` references **only** `Interfaces`. Pulling in `Models` or `Core`
  from a typed HTTP client would leak persistence types and business
  logic to every consumer.
- `Models` references `Interfaces` so entity DTO conversions are
  expressible with shared enums and value objects.
- `Core` references `Models` (transitively `Interfaces`). It also
  references the framework projects it needs (`FireflyFramework.Cqrs`,
  `FireflyFramework.Eda`, `FireflyFramework.Orchestration`, ...).
- `Web` references `Core` plus `FireflyFramework.Starter.Core`. It is
  the only project that pulls in the ASP.NET Core stack.

The compiler enforces the graph. There is no `csproj` knob to bypass
it — it is the layering.

---

## Per-project conventions

### `.Interfaces`

```
Dtos/V1/
  PlaceOrderRequest.cs
  OrderDto.cs
Enums/V1/
  OrderStatus.cs
```

- Use **versioned namespaces** (`*.V1`). Add `*.V2` for breaking changes
  and keep `*.V1` alive until callers migrate.
- DTOs are **records** — immutable, value-equality, `with` expressions.
- No project references. No `using` of any framework type. No I/O.
- This is the only assembly other services depend on.

### `.Models`

```
Entities/V1/
  OrderEntity.cs
Repositories/
  IOrderRepository.cs
  InMemoryOrderRepository.cs    # default for samples / dev
  OrderEfRepository.cs           # production: extends RepositoryBase<TEntity, TKey>
Configuration/
  OrderEntityConfiguration.cs   # IEntityTypeConfiguration<OrderEntity>
```

- Entities are **separate from DTOs** even when the field shapes look
  identical today. This decouples wire-format evolution from schema
  evolution.
- Repositories are declared as interfaces in this project. The default
  in-memory implementation is fine for samples and dev. Production code
  swaps in an EF Core implementation by referencing
  `FireflyFramework.Data` and inheriting `RepositoryBase<TEntity, TKey>`.
- Migrations live under `Migrations/` in this project, not in `.Web`.

### `.Core`

```
Mappers/
  OrderMapper.cs                # OrderEntity ↔ OrderDto
Services/Orders/V1/             # one folder per aggregate / domain
  PlaceOrderCommand.cs
  PlaceOrderHandler.cs
  GetOrderQuery.cs
  GetOrderHandler.cs
  OrderPlacedEvent.cs           # domain event
Authorizers/                    # optional: IAuthorizer<TCommand|TQuery>
Validators/                     # optional: dedicated validator if ValidateAsync grows
```

- All business logic goes through commands and queries dispatched on
  `ICommandBus` / `IQueryBus`. Do **not** create traditional `IFooService`
  classes — they bypass validation, caching, tracing, and authorization.
- Commands return `Task<TResult>`. Queries return `Task<TDto>` and may
  opt into per-query caching (`IsCacheable`, `CacheKey`, `CacheTtl`).
- Mappers are static extension methods (`entity.ToDto()`). For complex
  graphs use Mapperly or a dedicated profile class.

### `.Web`

```
Program.cs                      # AddFireflyCore + endpoint mapping
Controllers/V1/                 # if you prefer controllers
  OrdersController.cs
appsettings.json                # Firefly:* config
appsettings.Development.json
Dockerfile                      # multi-stage build
```

`Program.cs` should be tiny. The wiring is one call:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName: "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(PlaceOrderCommand).Assembly });

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

var app = builder.Build();
app.UseFireflyWeb();
app.MapControllers();   // or app.MapPost / app.MapGet
app.Run();
```

`AddFireflyCore` activates Web (RFC 7807, correlation IDs, idempotency,
PII masking), Cache, Observability (OpenTelemetry), EDA (in-memory by
default, swappable), and CQRS (handler discovery scoped to the supplied
assembly).

For richer needs, replace `AddFireflyCore` with one of:

| Starter call            | Adds on top of `AddFireflyCore`                                   |
|-------------------------|-------------------------------------------------------------------|
| `AddFireflyApplication` | `IExtensionRegistry` + `IPluginManager` (IDP / orchestration adapters are service-specific — register one of `KeycloakIdpAdapter` / `AzureAdIdpAdapter` / `CognitoIdpAdapter` / `InternalDbIdpAdapter` against `IIdpAdapter`) |
| `AddFireflyDomain`      | In-memory `IEventStore`. Replace with `EfCoreEventStore` for production. |
| `AddFireflyData`        | Adds Polly resilience helpers; the application registers its own `DbContext` via `services.AddDbContext<TDb>(...)`. |
| `AddFireflyBackOffice`  | Everything `AddFireflyApplication` adds plus the back-office context resolver and middleware. |

### `.Sdk`

```
IOrdersServiceClient.cs              # contract
OrdersServiceClient.cs               # typed HttpClient impl
OrdersServiceClientExtensions.cs     # AddOrdersServiceClient(...)
```

- The interface lives next to the implementation so consumers can mock
  it in tests.
- The DI extension registers a **typed** client (`AddHttpClient<I, T>`),
  which gives Polly resilience pipelines, named loggers, and DI of
  `IHttpClientFactory` plumbing for free.
- Never expose `Models` or `Core` types from this project — all parameter
  and return types must come from `Interfaces`.

---

## Naming and namespace rules

| Layer        | Namespace pattern                                                | Example                                                          |
|--------------|------------------------------------------------------------------|------------------------------------------------------------------|
| Interfaces   | `{Service}.Interfaces.{Dtos\|Enums}.{V1\|V2}.*`                   | `OrdersService.Interfaces.Dtos.V1.OrderDto`                      |
| Models       | `{Service}.Models.{Entities.V1\|Repositories\|Configuration}.*`  | `OrdersService.Models.Entities.V1.OrderEntity`                   |
| Core         | `{Service}.Core.{Services.{Domain}.V1\|Mappers}.*`               | `OrdersService.Core.Services.Orders.V1.PlaceOrderCommand`        |
| Web          | `{Service}.Web.{Controllers.V1}.*`                               | `OrdersService.Web.Controllers.V1.OrdersController`              |
| Sdk          | `{Service}.Sdk.*`                                                | `OrdersService.Sdk.OrdersServiceClient`                          |

API URL paths follow the same versioning: `/api/v1/orders/{id}`.

---

## Reference implementation

A complete five-project scaffold is in
[`samples/FireflyFramework.Samples.OrdersService.*`](../samples/). Read
it side-by-side with this document; every guideline above maps to a
concrete file there.

```
samples/
├── FireflyFramework.Samples.OrdersService.Interfaces/
├── FireflyFramework.Samples.OrdersService.Models/
├── FireflyFramework.Samples.OrdersService.Core/
├── FireflyFramework.Samples.OrdersService.Web/
└── FireflyFramework.Samples.OrdersService.Sdk/
```

---

## Bootstrapping a new service

1. Create five projects under `services/{your-service}/`:
   ```bash
   dotnet new classlib -n YourCompany.Catalog.Interfaces
   dotnet new classlib -n YourCompany.Catalog.Models
   dotnet new classlib -n YourCompany.Catalog.Core
   dotnet new web      -n YourCompany.Catalog.Web
   dotnet new classlib -n YourCompany.Catalog.Sdk
   ```
2. Wire references along the dependency graph above.
3. In `.Web`, reference `FireflyFramework.Starter.Core`
   (or `Starter.Application` / `Starter.Domain` / `Starter.Data` / `BackOffice`
   depending on what you need).
4. Add `appsettings.json` with the `Firefly:*` sections you need
   (see [`CONFIGURATION.md`](CONFIGURATION.md)).
5. Author your first command + handler in `.Core`. Add the matching
   endpoint in `.Web`. Done.

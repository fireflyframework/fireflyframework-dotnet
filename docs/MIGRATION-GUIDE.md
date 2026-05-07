# Java → .NET Migration Cookbook

> Side-by-side reference for engineers porting a Java/Spring Boot
> Firefly service to the .NET 10 stack.

## 1. Project layout

| Java                                                | .NET                                               |
|-----------------------------------------------------|----------------------------------------------------|
| `pom.xml` (multi-module reactor)                    | `FireflyFramework.sln` + per-project `*.csproj`    |
| `dependencyManagement` BoM                          | `Directory.Packages.props` (CPM)                   |
| `parent` POM                                        | `Directory.Build.props`                            |
| `src/main/java`                                     | `src/`                                             |
| `src/test/java`                                     | `tests/`                                           |
| `application.yaml`                                  | `appsettings.json` (+ `appsettings.{Env}.json`)    |

## 2. Reactive types

| Reactor                                   | .NET                                                       |
|-------------------------------------------|------------------------------------------------------------|
| `Mono<T>`                                 | `Task<T>` / `ValueTask<T>`                                 |
| `Flux<T>`                                 | `IAsyncEnumerable<T>`                                      |
| `Mono.empty()`                            | `Task.CompletedTask`                                       |
| `Mono.just(x)`                            | `Task.FromResult(x)`                                       |
| `mono.flatMap(...)`                       | `await ... ; await Inner(...)`                             |
| `mono.subscribeOn(boundedElastic)`        | `Task.Run(...)` (rare — use only for sync→async bridges)   |
| `flux.collectList()`                      | `await foreach ... { list.Add(...) }`                      |
| `flux.window(N)`                          | `Channel<T>` micro-batching pattern                        |

## 3. Spring DI ↔ Microsoft.Extensions.DependencyInjection

| Spring                                          | .NET                                                            |
|-------------------------------------------------|-----------------------------------------------------------------|
| `@Service` / `@Component` / `@Repository`       | Plain class + `services.AddScoped<I, Impl>()`                   |
| `@Configuration`                                | `IServiceCollection` extension method                           |
| `@Bean`                                         | `services.AddSingleton<T>(sp => new T(...))`                    |
| `@Autowired`                                    | Constructor parameter                                           |
| `@Qualifier("name")`                            | Keyed services: `services.AddKeyedScoped<I, Impl>("name")`      |
| `@Conditional` / `@ConditionalOnProperty`       | `if (config.GetValue<bool>(...)) services.Add...()`             |
| `@Scope("prototype")`                           | `services.AddTransient<T>()`                                    |
| `@PostConstruct`                                | `IHostedService.StartAsync` or factory function                 |
| `@PreDestroy`                                   | `IDisposable` / `IAsyncDisposable`                              |

## 4. Configuration

| Spring                                             | .NET                                                  |
|----------------------------------------------------|-------------------------------------------------------|
| `application.yaml`                                 | `appsettings.json`                                    |
| `@Value("${foo.bar}")`                             | `IConfiguration["Foo:Bar"]`                           |
| `@ConfigurationProperties("foo")`                  | `services.Configure<FooOptions>(cfg.GetSection("Foo"))` |
| Profiles (`application-dev.yaml`)                  | `appsettings.Development.json`                        |
| Spring Cloud Config Server                         | `Steeltoe.Configuration.ConfigServer.*`               |

## 5. Web layer

| Spring MVC / WebFlux                                  | ASP.NET Core 10                                            |
|-------------------------------------------------------|------------------------------------------------------------|
| `@RestController`                                     | `app.MapGet/Post/...` (minimal API) or `[ApiController]`   |
| `@RequestMapping("/users")`                           | `app.MapGroup("/users")`                                   |
| `@PathVariable Long id`                               | `(long id)` parameter binding                              |
| `@RequestBody`                                        | `[FromBody] T body`                                        |
| `@RequestParam`                                       | `[FromQuery]`                                              |
| `ResponseEntity.ok(body)`                             | `Results.Ok(body)`                                         |
| `@ExceptionHandler`                                   | `IExceptionHandler` + `app.UseExceptionHandler()`          |
| Filter / `@Aspect`                                    | Middleware (`app.Use(...)`) or `IEndpointFilter`           |
| `@Validated` + `@NotNull` etc.                        | DataAnnotations + `FluentValidation` for richer rules      |

## 6. Persistence

| Spring Data R2DBC                              | EF Core 10                                                |
|------------------------------------------------|-----------------------------------------------------------|
| `R2dbcRepository<T, Id>`                       | `DbContext` + `DbSet<T>`                                  |
| `@Table` / `@Id` / `@Column`                   | Same names — `[Table]` / `[Key]` / `[Column]`             |
| `Mono<T> findById(...)`                        | `await db.Set<T>().FindAsync(id)`                         |
| `Flux<T> findAllBy...`                         | `db.Set<T>().Where(...).AsAsyncEnumerable()`              |
| `@Transactional`                               | `await using var tx = await db.Database.BeginTransactionAsync();` |
| Liquibase / Flyway                             | EF Core migrations (`dotnet ef migrations add`)           |

## 7. CQRS

The Firefly CQRS contract is *intentionally* the same shape on both
sides:

```java
// Java
public class CreateOrder implements Command<UUID> { ... }

@Component
public class CreateOrderHandler implements CommandHandler<CreateOrder, UUID> {
    public Mono<UUID> handle(CreateOrder cmd, ExecutionContext ctx) { ... }
}
```

```csharp
// .NET
using FireflyFramework.Cqrs.Commands;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

public sealed record CreateOrder(string Sku, int Quantity) : ICommand<Guid>;

public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, Guid>
{
    public Task<Guid> HandleAsync(CreateOrder cmd, ExecutionContext ctx, CancellationToken ct) { ... }
}
```

Bus discovery:

* Java — `ApplicationContext` scans `@Component`s.
* .NET — `services.AddFireflyCqrs(typeof(Program).Assembly)` reflects
  every `ICommandHandler<,>` and `IQueryHandler<,>` implementation in
  the supplied assemblies and registers them as scoped services.

## 8. EDA — Kafka / RabbitMQ

| Spring                                            | .NET                                                       |
|---------------------------------------------------|------------------------------------------------------------|
| `KafkaTemplate<K, V>`                             | `IProducer<K, V>` (Confluent.Kafka)                        |
| `@KafkaListener`                                  | `KafkaEventConsumer` (`BackgroundService`)                 |
| `RabbitTemplate`                                  | `IConnection` + `IChannel` (RabbitMQ.Client 7.x)           |
| `@RabbitListener`                                 | `AsyncEventingBasicConsumer`                               |
| Confluent Schema Registry (`KafkaAvroSerializer`) | `SchemaRegistryAvroSerializer<T>`                          |
| Manual ack / nack                                 | `KafkaAckCallback.Acknowledge()` / `.Reject()`             |

## 9. Resilience

| Resilience4j                              | Polly v8                                                    |
|-------------------------------------------|-------------------------------------------------------------|
| `CircuitBreaker.of(...)`                  | `new ResiliencePipelineBuilder().AddCircuitBreaker(...)`    |
| `Retry.ofDefaults(...)`                   | `.AddRetry(new RetryStrategyOptions { ... })`               |
| `RateLimiter.of(...)`                     | `.AddRateLimiter(new SlidingWindowRateLimiter(...))`        |
| `Bulkhead`                                | `.AddConcurrencyLimiter(...)`                               |
| `TimeLimiter.of(...)`                     | `.AddTimeout(TimeSpan.FromSeconds(N))`                      |

## 10. Observability

| Java                                                       | .NET                                              |
|------------------------------------------------------------|---------------------------------------------------|
| Micrometer `MeterRegistry`                                 | `IMeterFactory` + `Meter` (`System.Diagnostics.Metrics`) |
| `@Timed` / `@Counted`                                      | `meter.CreateHistogram<double>(...)` (manual)     |
| Spring Sleuth / Brave                                      | `ActivitySource` + OpenTelemetry .NET             |
| Logback                                                    | `Microsoft.Extensions.Logging` + Serilog          |

## 11. Validation

| Bean Validation 3.0 (`jakarta.validation`)        | DataAnnotations + FluentValidation                |
|---------------------------------------------------|---------------------------------------------------|
| `@NotNull String x`                               | `[Required]` / `RuleFor(x => x.X).NotNull()`      |
| `@Size(min=1)`                                    | `[MinLength(1)]` / `.MinimumLength(1)`            |
| `@Pattern(regexp="...")`                          | `[RegularExpression("...")]` / `.Matches("...")`  |
| `@Email`                                          | `[EmailAddress]` / `.EmailAddress()`              |
| Custom validator (`ConstraintValidator<...>`)     | A subclass of `ValidationAttribute` or a FluentValidation rule |

## 12. Testing

| Java                          | .NET                                          |
|-------------------------------|-----------------------------------------------|
| JUnit 5                       | xUnit                                         |
| Mockito                       | Moq / NSubstitute                             |
| AssertJ                       | FluentAssertions                              |
| Testcontainers                | Testcontainers .NET                           |
| `@SpringBootTest`             | `WebApplicationFactory<TStartup>`             |
| `@DataR2dbcTest`              | `services.AddDbContext<...>(o => o.UseInMemoryDatabase(...))` |

## 13. Identity

The framework ships four IDP adapters (one per provider). Pick one and
register it as a singleton against the `IIdpAdapter` port.

| Java side                                | .NET (in this framework)                                       |
|------------------------------------------|----------------------------------------------------------------|
| `firefly-idp-keycloak`                   | `KeycloakIdpAdapter` (`FireflyFramework.Idp.Keycloak`)         |
| `firefly-idp-azure-ad`                   | `AzureAdIdpAdapter` (`FireflyFramework.Idp.AzureAd`)           |
| `firefly-idp-aws-cognito`                | `CognitoIdpAdapter` (`FireflyFramework.Idp.AwsCognito`)        |
| `firefly-idp-internal-db`                | `InternalDbIdpAdapter` (`FireflyFramework.Idp.InternalDb`)     |
| Spring `OAuth2ResourceServerConfigurer`  | `services.AddAuthentication().AddJwtBearer(...)`               |

## 14. Quick service skeleton (.NET)

The canonical Firefly service uses the five-project layout documented
in [`SERVICE-SCAFFOLDING.md`](SERVICE-SCAFFOLDING.md) and the
`AddFireflyCore` starter.

The runnable reference lives at
[`samples/FireflyFramework.Samples.OrdersService.*`](../samples/);
its `.Web/Program.cs`:

```csharp
using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Samples.OrdersService.Core.Services.Orders.V1;
using FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;
using FireflyFramework.Samples.OrdersService.Models.Repositories;
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.DependencyInjection;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(PlaceOrderCommand).Assembly });

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseFireflyWeb();   // RFC 7807 + correlation id + idempotency middleware
app.MapOpenApi();

app.MapPost("/api/v1/orders", async (PlaceOrderRequest req, ICommandBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "demo-user", TenantId = "demo-tenant" };
    var orderId = await bus.SendAsync(new PlaceOrderCommand(req.Sku, req.Quantity, req.UnitPrice), ctx, ct);
    return Results.Created($"/api/v1/orders/{orderId}", new { orderId });
});

await app.RunAsync();
```

That replaces the Spring Boot `@SpringBootApplication` +
`Application.run(args)` entry point and the conventional
`application.yaml`. Substitute `AddFireflyApplication`,
`AddFireflyDomain`, `AddFireflyData`, or `AddFireflyBackOffice` when
the service needs the additional registrations they bring in.

using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Queries;
using FireflyFramework.Cqrs.Validation;
using FireflyFramework.Samples.OrdersService;
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.DependencyInjection;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

var builder = WebApplication.CreateBuilder(args);

// One-line wiring of the entire Firefly infrastructure tier.
builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName: "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

builder.Services.AddSingleton<InMemoryOrderRepository>();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseFireflyWeb();
app.MapOpenApi();

// === Sample endpoints ===
app.MapPost("/api/orders", async (PlaceOrderRequest request, ICommandBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "demo-user", TenantId = "demo-tenant" };
    var orderId = await bus.SendAsync(new PlaceOrderCommand(request.Sku, request.Quantity, request.UnitPrice), ctx, ct);
    return Results.Created($"/api/orders/{orderId}", new { orderId });
});

app.MapGet("/api/orders/{id:guid}", async (Guid id, IQueryBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "demo-user" };
    var order = await bus.AskAsync(new GetOrderQuery(id), ctx, ct);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();

public partial class Program;

namespace FireflyFramework.Samples.OrdersService
{
    public sealed record PlaceOrderRequest(string Sku, int Quantity, decimal UnitPrice);

    public sealed record OrderDto(Guid Id, string Sku, int Quantity, decimal Total, string Status);

    public sealed record PlaceOrderCommand(string Sku, int Quantity, decimal UnitPrice) : ICommand<Guid>
    {
        public Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(Sku))
            {
                return Task.FromResult(ValidationResult.Failed("Sku", "SKU is required"));
            }

            if (Quantity <= 0)
            {
                return Task.FromResult(ValidationResult.Failed("Quantity", "Quantity must be > 0"));
            }

            if (UnitPrice <= 0)
            {
                return Task.FromResult(ValidationResult.Failed("UnitPrice", "UnitPrice must be > 0"));
            }

            return Task.FromResult(ValidationResult.Successful());
        }
    }

    public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderDto?>
    {
        public bool IsCacheable => true;
        public string? CacheKey => $"order:{OrderId}";
        public TimeSpan? CacheTtl => TimeSpan.FromMinutes(5);
    }

    public sealed class PlaceOrderHandler(InMemoryOrderRepository repo) : ICommandHandler<PlaceOrderCommand, Guid>
    {
        public Task<Guid> HandleAsync(PlaceOrderCommand command, ExecutionContext context, CancellationToken ct = default)
        {
            var dto = new OrderDto(Guid.NewGuid(), command.Sku, command.Quantity, command.Quantity * command.UnitPrice, "Placed");
            repo.Save(dto);
            return Task.FromResult(dto.Id);
        }
    }

    public sealed class GetOrderHandler(InMemoryOrderRepository repo) : IQueryHandler<GetOrderQuery, OrderDto?>
    {
        public Task<OrderDto?> HandleAsync(GetOrderQuery query, ExecutionContext context, CancellationToken ct = default) =>
            Task.FromResult(repo.Get(query.OrderId));
    }

    public sealed class InMemoryOrderRepository
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, OrderDto> _store = new();
        public void Save(OrderDto dto) => _store[dto.Id] = dto;
        public OrderDto? Get(Guid id) => _store.TryGetValue(id, out var dto) ? dto : null;
    }
}

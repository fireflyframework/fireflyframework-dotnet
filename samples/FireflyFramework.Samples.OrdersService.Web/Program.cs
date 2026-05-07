using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Samples.OrdersService.Core.Services.Orders.V1;
using FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;
using FireflyFramework.Samples.OrdersService.Models.Repositories;
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.DependencyInjection;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

var builder = WebApplication.CreateBuilder(args);

// One-line wiring of the entire Firefly infrastructure tier.
builder.Services.AddFireflyCore(
    builder.Configuration,
    serviceName: "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(PlaceOrderCommand).Assembly });

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseFireflyWeb();
app.MapOpenApi();

app.MapPost("/api/v1/orders", async (PlaceOrderRequest request, ICommandBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "demo-user", TenantId = "demo-tenant" };
    var orderId = await bus.SendAsync(new PlaceOrderCommand(request.Sku, request.Quantity, request.UnitPrice), ctx, ct);
    return Results.Created($"/api/v1/orders/{orderId}", new { orderId });
});

app.MapGet("/api/v1/orders/{id:guid}", async (Guid id, IQueryBus bus, CancellationToken ct) =>
{
    var ctx = new ExecutionContext { UserId = "demo-user" };
    var order = await bus.AskAsync(new GetOrderQuery(id), ctx, ct);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();

public partial class Program;

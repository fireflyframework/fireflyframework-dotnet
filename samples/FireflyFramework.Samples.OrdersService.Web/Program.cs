// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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

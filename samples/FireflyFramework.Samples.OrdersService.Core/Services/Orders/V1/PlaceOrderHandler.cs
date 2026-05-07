using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Samples.OrdersService.Interfaces.Enums.V1;
using FireflyFramework.Samples.OrdersService.Models.Entities.V1;
using FireflyFramework.Samples.OrdersService.Models.Repositories;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

namespace FireflyFramework.Samples.OrdersService.Core.Services.Orders.V1;

public sealed class PlaceOrderHandler(IOrderRepository repository) : ICommandHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> HandleAsync(PlaceOrderCommand command, ExecutionContext context, CancellationToken ct = default)
    {
        var entity = new OrderEntity(
            Id: Guid.NewGuid(),
            Sku: command.Sku,
            Quantity: command.Quantity,
            UnitPrice: command.UnitPrice,
            Total: command.Quantity * command.UnitPrice,
            Status: OrderStatus.Placed,
            CreatedAt: DateTimeOffset.UtcNow);

        var saved = await repository.SaveAsync(entity, ct).ConfigureAwait(false);
        return saved.Id;
    }
}

using FireflyFramework.Cqrs.Queries;
using FireflyFramework.Samples.OrdersService.Core.Mappers;
using FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;
using FireflyFramework.Samples.OrdersService.Models.Repositories;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

namespace FireflyFramework.Samples.OrdersService.Core.Services.Orders.V1;

public sealed class GetOrderHandler(IOrderRepository repository) : IQueryHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> HandleAsync(GetOrderQuery query, ExecutionContext context, CancellationToken ct = default)
    {
        var entity = await repository.GetAsync(query.OrderId, ct).ConfigureAwait(false);
        return entity?.ToDto();
    }
}

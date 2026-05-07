using FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;
using FireflyFramework.Samples.OrdersService.Models.Entities.V1;

namespace FireflyFramework.Samples.OrdersService.Core.Mappers;

public static class OrderMapper
{
    public static OrderDto ToDto(this OrderEntity entity) =>
        new(entity.Id, entity.Sku, entity.Quantity, entity.Total, entity.Status);
}

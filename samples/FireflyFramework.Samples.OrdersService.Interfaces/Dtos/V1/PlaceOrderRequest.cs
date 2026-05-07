namespace FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;

public sealed record PlaceOrderRequest(string Sku, int Quantity, decimal UnitPrice);

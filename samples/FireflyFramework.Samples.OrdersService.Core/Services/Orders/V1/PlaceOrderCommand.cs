using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.Validation;

namespace FireflyFramework.Samples.OrdersService.Core.Services.Orders.V1;

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

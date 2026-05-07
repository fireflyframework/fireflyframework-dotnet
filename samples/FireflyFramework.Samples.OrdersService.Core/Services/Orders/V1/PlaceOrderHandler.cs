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

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

using System.Net;
using System.Net.Http.Json;
using FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;

namespace FireflyFramework.Samples.OrdersService.Sdk;

public sealed class OrdersServiceClient(HttpClient http) : IOrdersServiceClient
{
    public async Task<Guid> PlaceOrderAsync(PlaceOrderRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/orders")
        {
            Content = JsonContent.Create(request),
        };
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        }

        using var response = await http.SendAsync(message, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty response from orders service.");
        return payload.OrderId;
    }

    public async Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/v1/orders/{id}", ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct).ConfigureAwait(false);
    }

    private sealed record PlaceOrderResponse(Guid OrderId);
}

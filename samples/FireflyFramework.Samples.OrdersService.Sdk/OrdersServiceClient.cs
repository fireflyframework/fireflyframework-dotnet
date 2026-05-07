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

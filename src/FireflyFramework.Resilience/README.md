# FireflyFramework.Resilience

Standalone resilience module porting Java `Resilience4j` and pyfly
`resilience`. Built on Polly v8.

## Patterns provided

| Pattern | Attribute | Polly strategy |
|---|---|---|
| Circuit breaker | `[CircuitBreaker]` | `AddCircuitBreaker` |
| Retry | `[Retry]` | `AddRetry` |
| Rate limiter | `[RateLimiter]` | `AddRateLimiter` (sliding window) |
| Bulkhead | `[Bulkhead]` | `AddConcurrencyLimiter` |
| Time limiter | `[TimeLimiter]` | `AddTimeout` |
| Fallback | `[Fallback]` | post-pipeline fallback method dispatch |

## Why a separate module?

In the Java framework the resilience layer is *cross-cutting*: any
caller (CQRS handlers, EDA consumers, IDP adapter, ECM storage) can
declaratively wrap an operation. The .NET port previously had Polly
embedded inside the `Client` project only. Pulling it out as
`FireflyFramework.Resilience` mirrors the Java `fireflyframework-utils` /
`Resilience4j` split so any module can take a dependency without
pulling the entire HTTP client stack.

## Quick start

```csharp
services.AddFireflyResilience(Configuration);
```

```yaml
Firefly:
  Resilience:
    CircuitBreakers:
      orders:
        FailureRateThreshold: 0.5
        SamplingDuration: 00:00:30
        MinimumThroughput: 20
    Retries:
      payments:
        MaxAttempts: 3
        Delay: 00:00:00.5
        BackoffType: Exponential
        UseJitter: true
```

```csharp
public sealed class PaymentService
{
    private readonly IResilienceRegistry _registry;
    public PaymentService(IResilienceRegistry r) => _registry = r;

    public Task<Receipt> ChargeAsync(Order o, CancellationToken ct) =>
        _registry.GetPipeline("payments").ExecuteAsync(
            async token => await _gateway.ChargeAsync(o, token), ct).AsTask();
}
```

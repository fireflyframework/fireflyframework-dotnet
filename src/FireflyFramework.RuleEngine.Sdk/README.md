# FireflyFramework.RuleEngine.Sdk

## Overview

`FireflyFramework.RuleEngine.Sdk` is the **typed HTTP client** for the
rule-engine REST API. Use it from any .NET service that needs to
evaluate a centrally-managed rule remotely without pulling in the
parser, the AST, or the visitor evaluator.

The SDK is two interfaces and two implementations: `IRuleEngineClient`
and its concrete `RuleEngineClient`, plus a one-line registration
extension. It rides on top of `HttpClient` and `System.Net.Http.Json`,
so it inherits `Microsoft.Extensions.Http`'s message-handler pipeline
— observability, retries, circuit breakers, mTLS — without any extra
ceremony.

Mirrors `org.fireflyframework:firefly-common-rule-engine-sdk` (the
Spring Cloud OpenFeign client). The wire format and endpoint paths
are identical.

## Why a separate module?

A consumer that wants to call the rule engine over HTTP doesn't need
the parser or the evaluator. The SDK depends only on
`RuleEngine.Interfaces`, so referencing it pulls in a single
~30 KB dependency-free assembly plus `HttpClient` plumbing. By
contrast, referencing `RuleEngine.Core` directly would drag in
YamlDotNet and the visitor machinery — a megabyte of indirect
dependencies you don't need at the call site.

## Mental model

```
   your service                                Rule engine deployment
        │                                              ▲
        │ calls                                        │
        ▼                                              │
   IRuleEngineClient ──── HttpClient ────────► /api/rules/evaluate/*
   (this module)               │
                               │
                       message-handler pipeline:
                       ├── correlation-id header
                       ├── auth header
                       ├── Polly retry
                       ├── circuit breaker
                       └── OpenTelemetry span
```

Treat `IRuleEngineClient` as a thin transport layer. Add resilience
and tracing through `IHttpClientBuilder` extensions — the same way
you'd treat any other typed HttpClient.

## Quick start

```csharp
using FireflyFramework.RuleEngine.Sdk;

builder.Services.AddRuleEngineClient(new Uri("https://rules.svc.local"));
```

Then inject `IRuleEngineClient` anywhere:

```csharp
using FireflyFramework.RuleEngine.Interfaces;
using FireflyFramework.RuleEngine.Sdk;

public sealed class CheckoutPricing(IRuleEngineClient rules)
{
    public async Task<decimal> ApplyVipDiscountAsync(decimal amount, bool isVip, CancellationToken ct)
    {
        var response = await rules.EvaluateByCodeAsync(
            new RuleEvaluationByCodeRequestDto(
                RuleCode:  "vip-discount",
                InputData: new Dictionary<string, object?>
                {
                    ["amount"] = amount,
                    ["isVip"]  = isVip,
                }),
            ct);

        if (response is not { Success: true } || !response.Output.TryGetValue("discount", out var d))
            return 0m;

        return d switch
        {
            decimal v => v,
            double  v => (decimal)v,
            _         => 0m,
        };
    }
}
```

## Public surface

| Member                                                                  | Calls                                                |
|-------------------------------------------------------------------------|------------------------------------------------------|
| `IRuleEngineClient.EvaluateAsync(RulesEvaluationRequestDto)`            | `POST /api/rules/evaluate/direct` (base-64 YAML)    |
| `IRuleEngineClient.EvaluatePlainAsync(PlainYamlEvaluationRequestDto)`   | `POST /api/rules/evaluate/plain` (plain-text YAML)  |
| `IRuleEngineClient.EvaluateByCodeAsync(RuleEvaluationByCodeRequestDto)` | `POST /api/rules/evaluate/by-code`                  |
| `AddRuleEngineClient(IServiceCollection, Uri)`                          | Registers `IRuleEngineClient` + `RuleEngineClient`   |

All three methods return `RulesEvaluationResponseDto?`. Non-2xx
responses throw `HttpRequestException` via
`HttpResponseMessage.EnsureSuccessStatusCode()`.

## Common patterns

### Adding Polly v8 resilience

```csharp
using Microsoft.Extensions.Http.Resilience;

builder.Services.AddRuleEngineClient(new Uri("https://rules.svc.local"))
    .AddStandardResilienceHandler(o =>
    {
        o.Retry.MaxRetryAttempts = 3;
        o.Retry.Delay            = TimeSpan.FromMilliseconds(200);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
    });
```

The "standard resilience handler" composes the Microsoft-recommended
defaults: bulkhead → total-request-timeout → retry → circuit-breaker
→ per-attempt-timeout. For most rule-engine traffic this is a sane
default — rule evaluations are pure functions of their input, so
retries are safe.

### Wiring an auth header

```csharp
builder.Services.AddRuleEngineClient(new Uri("https://rules.svc.local"))
    .AddHttpMessageHandler(sp =>
    {
        var tokens = sp.GetRequiredService<IOAuth2TokenCache>();
        return new BearerTokenHandler(tokens, audience: "rule-engine");
    });
```

The token cache (from `FireflyFramework.Client`) refreshes the JWT
before expiry; the handler injects it on every outbound request.

### Streaming many evaluations

When a service needs to evaluate hundreds of rules concurrently
(e.g. a bulk pricing job), prefer the batch DTO:

```csharp
var batch = new BatchRulesEvaluationRequestDto(
    Evaluations: orders.Select(o => new RulesEvaluationRequestDto(
        Base64YamlContent: encodedYaml,
        InputData:         new() { ["amount"] = o.Total })).ToList(),
    ConcurrencyLimit: 16,
    TimeoutMs:        30_000);

// Note: the SDK doesn't expose a typed batch method out of the box —
// post the DTO via SendAsync to /api/rules/evaluate/batch directly,
// or extend RuleEngineClient with a method:

public async Task<BatchRulesEvaluationResponseDto?> EvaluateBatchAsync(
    BatchRulesEvaluationRequestDto request, CancellationToken ct = default)
{
    using var resp = await _http.PostAsJsonAsync("api/rules/evaluate/batch", request, ct);
    resp.EnsureSuccessStatusCode();
    return await resp.Content.ReadFromJsonAsync<BatchRulesEvaluationResponseDto>(ct);
}
```

### Health-check probe

A simple liveness probe that doesn't need a real rule:

```csharp
builder.Services.AddHealthChecks()
    .AddTypedClient<IRuleEngineClient>("rule-engine", async (client, ct) =>
    {
        var resp = await client.EvaluatePlainAsync(
            new PlainYamlEvaluationRequestDto(
                YamlContent: "ruleName: ping\n",
                InputData:   new()),
            ct);
        return resp?.Success == true ? HealthCheckResult.Healthy() : HealthCheckResult.Degraded();
    });
```

## Pitfalls and gotchas

- **`EnsureSuccessStatusCode` throws on 4xx as well as 5xx.** A
  malformed YAML or unknown rule-code surfaces as a 400, which becomes
  a `HttpRequestException`. If you want to distinguish "rule said no"
  from "transport error," catch the exception and inspect the
  status code — or pull the `ProblemDetail` body off the response
  before EnsureSuccessStatusCode.
- **`RulesEvaluationResponseDto?` can be `null` even on 2xx.** That
  happens only if the server returns an empty body; treat it as a
  protocol bug if you see it. The framework never returns 200 with no
  body.
- **`EvaluateAsync` requires base-64 YAML.** If you pass raw YAML to
  the `direct` endpoint, the server rejects it. Use `EvaluatePlainAsync`
  for the plain-text variant.
- **Order matters in the dictionary.** `InputData` is
  `Dictionary<string, object?>`. JSON serialisation preserves
  insertion order, but the rule engine doesn't depend on order. If
  you need deterministic output for testing, sort the keys before
  serialisation.
- **Cancellation tokens are passed through.** A cancelled call
  aborts both the in-flight HTTP request and the server-side
  evaluation (the server respects `HttpContext.RequestAborted`).

## Internals (for the curious)

- `RuleEngineClient.PostAsync<T>` is the only HTTP shim — every
  public method delegates to it. This keeps a single point for adding
  cross-cutting behaviour (logging, span tagging, request validation).
- `AddRuleEngineClient` returns `IHttpClientBuilder` so the caller
  can layer additional handlers (resilience, auth, OpenTelemetry)
  fluently.
- `System.Net.Http.Json` ships in the .NET framework — no extra
  package import. The default `JsonSerializerOptions` use the
  property-name casing of the DTOs, so the wire JSON matches the
  Java line.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.RuleEngine.Interfaces` | DTO shapes                          |
| `Microsoft.Extensions.Http`              | `AddHttpClient<TClient, TImpl>`     |

`System.Net.Http.Json` ships in the .NET framework — no package
import needed.

## Java mapping

| .NET                    | Java                                |
|-------------------------|-------------------------------------|
| `IRuleEngineClient`     | `RuleEngineClient` (interface)      |
| `RuleEngineClient`      | `RuleEngineClient`                  |
| `AddRuleEngineClient`   | Spring Cloud OpenFeign auto-config  |

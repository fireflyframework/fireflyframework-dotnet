# FireflyFramework.RuleEngine.Sdk

Typed `HttpClient` for the rule-engine REST API exposed by
`FireflyFramework.RuleEngine.Web`. Use it from any .NET service that
needs to evaluate a centrally-managed rule remotely without pulling in
the evaluator engine.

Mirrors `org.fireflyframework:firefly-common-rule-engine-sdk`.

## Wiring

```csharp
using FireflyFramework.RuleEngine.Sdk;

builder.Services.AddRuleEngineClient(new Uri("https://rules.svc.local"));
```

`AddRuleEngineClient` registers `IRuleEngineClient` against a typed
`HttpClient` — the same shape as the canonical service Sdk in
[`samples/FireflyFramework.Samples.OrdersService.Sdk`](../../samples/FireflyFramework.Samples.OrdersService.Sdk).

## Usage

```csharp
using FireflyFramework.RuleEngine.Interfaces;
using FireflyFramework.RuleEngine.Sdk;

public sealed class CheckoutPricing(IRuleEngineClient rules)
{
    public Task<RulesEvaluationResponseDto?> ApplyVipDiscount(decimal amount, bool isVip, CancellationToken ct) =>
        rules.EvaluateByCodeAsync(
            new RuleEvaluationByCodeRequestDto(
                RuleCode:  "vip-discount",
                InputData: new Dictionary<string, object?> { ["amount"] = amount, ["isVip"] = isVip }),
            ct);
}
```

## Public surface

| Member                                                                  | Calls                                                |
|-------------------------------------------------------------------------|------------------------------------------------------|
| `IRuleEngineClient.EvaluateAsync(RulesEvaluationRequestDto)`            | `POST /api/rules/evaluate/direct` (base-64 YAML)    |
| `IRuleEngineClient.EvaluatePlainAsync(PlainYamlEvaluationRequestDto)`   | `POST /api/rules/evaluate/plain` (plain-text YAML)  |
| `IRuleEngineClient.EvaluateByCodeAsync(RuleEvaluationByCodeRequestDto)` | `POST /api/rules/evaluate/by-code`                  |
| `AddRuleEngineClient(IServiceCollection, Uri)`                          | Registers `IRuleEngineClient` + `RuleEngineClient`   |

All three methods return `RulesEvaluationResponseDto?`. Non-success
responses throw `HttpRequestException` via `EnsureSuccessStatusCode`.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.RuleEngine.Interfaces` | DTO shapes                          |
| `Microsoft.Extensions.Http`              | `AddHttpClient<TClient, TImpl>`     |

`System.Net.Http.Json` ships in the .NET 10 framework — no package
import needed.

## Java mapping

| .NET                    | Java                                |
|-------------------------|-------------------------------------|
| `IRuleEngineClient`     | `RuleEngineClient` (interface)      |
| `RuleEngineClient`      | `RuleEngineClient`                  |
| `AddRuleEngineClient`   | Spring Cloud OpenFeign auto-config  |

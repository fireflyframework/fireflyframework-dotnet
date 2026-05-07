# FireflyFramework.RuleEngine.Sdk

Typed `HttpClient` wrapper that calls the rule-engine REST API exposed
by `FireflyFramework.RuleEngine.Web`. Use it from any .NET service that
needs to evaluate a centrally-managed rule remotely.

Mirrors `org.fireflyframework:firefly-common-rule-engine-sdk`.

## Usage

```csharp
using FireflyFramework.RuleEngine.Interfaces;
using FireflyFramework.RuleEngine.Sdk;

builder.Services
    .AddHttpClient<RuleEngineClient>(c => c.BaseAddress = new Uri("https://rules.svc.local"));

// Inject and call:
var response = await ruleEngineClient.EvaluateByCodeAsync(
    new RuleEvaluationByCodeRequestDto(
        Code:  "vip-discount",
        Input: new Dictionary<string, object?> { ["amount"] = 1500m, ["isVip"] = true }),
    ct);
```

## Public surface

| Method                  | Calls                                                  |
|-------------------------|--------------------------------------------------------|
| `EvaluateAsync`         | `POST /api/rules/evaluate/direct`                      |
| `EvaluateByCodeAsync`   | `POST /api/rules/evaluate/by-code`                     |

Both return `RulesEvaluationResponseDto?`.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.RuleEngine.Interfaces` | DTOs                           |
| `System.Net.Http.Json`                   | Typed JSON HTTP                |

## Java mapping

| .NET                | Java                              |
|---------------------|-----------------------------------|
| `RuleEngineClient`  | `RuleEngineClient`                |

# FireflyFramework.RuleEngine.Web

ASP.NET Core controller that exposes the rule engine over HTTP. Mirrors
`org.fireflyframework:firefly-common-rule-engine-web`.

## Endpoints

| Method | Path                            | Body                                | Description                                                |
|--------|---------------------------------|-------------------------------------|------------------------------------------------------------|
| POST   | `/api/rules/evaluate/direct`    | `RulesEvaluationRequestDto`         | Evaluate a base-64-encoded YAML rule against an input map  |
| POST   | `/api/rules/evaluate/plain`     | `PlainYamlEvaluationRequestDto`     | Evaluate a plain-text YAML rule against an input map       |
| POST   | `/api/rules/evaluate/by-code`   | `RuleEvaluationByCodeRequestDto`    | Look the rule up by its stable `code` and evaluate         |

The controller delegates to `IRulesEvaluationService` from
`RuleEngine.Core`, so the underlying behaviour is identical to direct
in-process use.

## Wiring

```csharp
using FireflyFramework.RuleEngine.Core;
using FireflyFramework.RuleEngine.Web.Controllers;

builder.Services.AddSingleton<IRulesEvaluationService, RulesEvaluationService>();
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(RulesEvaluationController).Assembly);
```

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.RuleEngine.Core`       | `IRulesEvaluationService`           |
| `FireflyFramework.RuleEngine.Interfaces` | DTOs                                |
| `Microsoft.AspNetCore.App`               | `[ApiController]`, MVC binding      |

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `RulesEvaluationController`   | `RulesEvaluationController`         |

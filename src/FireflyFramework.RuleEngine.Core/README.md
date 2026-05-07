# FireflyFramework.RuleEngine.Core

YAML rule DSL with AST + visitor evaluator. Mirrors
`org.fireflyframework:firefly-common-rule-engine-core`.

## DSL example

```yaml
ruleName: vip-discount
version: '1'
inputVariables:
  amount: number
  isVip:  boolean
conditions:
  - $amount > 500
  - and:
      - $isVip == true
      - $amount < 10000
actions:
  - $discount = 0.15
  - $tier = "gold"
```

## Evaluating a rule

```csharp
using FireflyFramework.RuleEngine.Core.Dsl;
using FireflyFramework.RuleEngine.Core.Engine;

var parser = new YamlDslParser();
var ast    = parser.Parse(yaml);

var ctx    = new EvaluationContext();
var engine = new AstRulesEvaluationEngine(ctx);
var result = engine.Evaluate(ast, new Dictionary<string, object?>
{
    ["amount"] = 1500m,
    ["isVip"]  = true,
});

// result.Success            -> true
// result.VariableValues["discount"] -> 0.15m
// result.VariableValues["tier"]     -> "gold"
// result.ExecutedActions             -> ["$discount = 0.15", "$tier = \"gold\""]
```

## Public surface

| Layer            | Types                                                                        |
|------------------|------------------------------------------------------------------------------|
| AST nodes        | `Expression`, `Condition`, `Action` hierarchies covering binary / unary / variable / literal / function / arithmetic / comparison / logical / assignment / conditional / for-each / while constructs |
| Visitor pattern  | `IAstVisitor<T>` with one method per node type                               |
| Parsing          | `YamlDslParser` (YamlDotNet-based) builds an `AstRulesDsl` from YAML         |
| Evaluation       | `AstRulesEvaluationEngine` implements `IAstVisitor<object?>` and runs the rule against an `EvaluationContext` |
| Services         | `IRulesEvaluationService`, `IBatchRulesEvaluationService`, `IRuleDefinitionService`, `IConstantService`, `IAuditTrailService` |

`RulesEvaluationService` exposes three entry points:

- `EvaluateRulesDirectAsync(base64YamlContent, input)` — when the rule
  body is supplied inline as base-64-encoded YAML.
- `EvaluateRulesPlainAsync(plainYaml, input)` — same, with plain text YAML.
- `EvaluateRuleByCodeAsync(ruleCode, input)` — looks the rule up via
  `IRuleDefinitionService`.

## Result shape

```csharp
public sealed record AstRulesEvaluationResult(
    bool Success,
    Dictionary<string, object?> Output,
    List<string>                 ExecutedActions,
    Dictionary<string, object?>  VariableValues,
    string?                      ErrorMessage = null);
```

## Dependencies

| Reference                                 | Used for                  |
|-------------------------------------------|---------------------------|
| `FireflyFramework.Kernel`                 | Base exceptions           |
| `FireflyFramework.RuleEngine.Interfaces`  | DTOs                      |
| `FireflyFramework.RuleEngine.Models`      | EF Core entities          |
| `YamlDotNet`                              | YAML parser               |

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `AstRulesEvaluationEngine`    | `ASTRulesEvaluationEngine`          |
| `YamlDslParser`               | `YamlDslParser`                     |
| `RulesEvaluationService`      | `RulesEvaluationService`            |
| `EvaluationContext`           | `EvaluationContext`                 |
| `IAstVisitor<T>`              | `ASTVisitor<T>`                     |

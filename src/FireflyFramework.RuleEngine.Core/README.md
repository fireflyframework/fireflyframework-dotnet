# FireflyFramework.RuleEngine.Core

YAML rule DSL with AST + visitor evaluator. Mirrors `fireflyframework-rule-engine-core`.

## DSL

```yaml
ruleName: vip-discount
version: '1'
inputVariables:
  amount: number
  isVip: boolean
conditions:
  - $amount > 500
  - and:
      - $isVip == true
      - $amount < 10000
actions:
  - $discount = 0.15
  - $tier = "gold"
```

## Quick start

```csharp
var parser = new YamlDslParser();
var ast = parser.Parse(yaml);

var ctx = new EvaluationContext();
var engine = new AstRulesEvaluationEngine(ctx);
var result = engine.Evaluate(ast, new Dictionary<string, object?>
{
    ["amount"] = 1500m,
    ["isVip"] = true,
});

result.Success.Should().BeTrue();
result.VariableValues["discount"].Should().Be(0.15m);
result.VariableValues["tier"].Should().Be("gold");
```

## What's inside

| Layer | Types |
|---|---|
| AST nodes | `Expression`, `Condition`, `Action` hierarchies — 15 node types covering binary / unary / variable / literal / function / arithmetic / comparison / logical / assignment / conditional / for-each / while |
| Visitor pattern | `IAstVisitor<T>` with one method per node type |
| Parsing | `YamlDslParser` (YamlDotNet-based) builds an `AstRulesDsl` from YAML |
| Evaluation | `AstRulesEvaluationEngine` implements `IAstVisitor<object?>` and runs the rule against an `EvaluationContext` |
| Services | `IRulesEvaluationService`, `IBatchRulesEvaluationService`, `IRuleDefinitionService`, `IConstantService`, `IAuditTrailService` |

`RulesEvaluationService` uses the parser + evaluator and supports three entry points: `EvaluateRulesDirectAsync` (base64 YAML), `EvaluateRulesPlainAsync` (plain YAML), `EvaluateRuleByCodeAsync` (looks up the rule via `IRuleDefinitionService`).

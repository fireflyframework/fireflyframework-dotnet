# FireflyFramework.RuleEngine.Core

## Overview

`FireflyFramework.RuleEngine.Core` is the **rule-engine runtime tier**:
the YAML DSL parser, the AST node hierarchy, the visitor-pattern
evaluator, and the service-layer interfaces (`IRulesEvaluationService`,
`IRuleDefinitionService`, `IConstantService`, `IAuditTrailService`)
that the REST controller and SDK call into.

The engine evaluates a rule against an input dictionary, mutating an
`EvaluationContext` along the way, and returns the final variable map
plus the list of actions that fired. Rules are written in YAML so
operators can edit them in source control or through an admin UI
without a re-deploy of the host service.

Mirrors `org.fireflyframework:firefly-common-rule-engine-core` on the
Java side. The DSL is identical (same operators, same operator
spellings); the AST node names are direct translations; the
evaluator's behaviour is intentionally bug-compatible.

## Why a separate module?

Rule engines occupy an awkward niche: the rule body is *data* (it
ships with deployment, can be edited by non-developers, lives in a
database row), but evaluation is *code* (it must run with full type
fidelity). Putting the parser and evaluator in their own assembly:

- Lets services that *invoke* rules over HTTP (using
  `RuleEngine.Sdk`) avoid pulling in YamlDotNet and the visitor
  machinery.
- Lets a custom storage backend (`IRuleDefinitionStore`) be developed
  against `Core` without re-implementing the parser.
- Ensures every consumer evaluates rules with the same semantics —
  there's a single canonical `AstRulesEvaluationEngine`.

## Mental model

```
        YAML text                              caller's input data
            │                                          │
            ▼                                          │
   ┌─────────────────────┐                             │
   │   YamlDslParser     │                             │
   │   YamlDotNet-based  │                             │
   └────────┬────────────┘                             │
            │ AstRulesDsl (typed AST)                  │
            ▼                                          │
   ┌────────────────────────────────────┐              │
   │  AstRulesEvaluationEngine          │ ◄────────────┘
   │  IAstVisitor<object?>              │
   │  walks Conditions then Actions     │
   └────────┬───────────────────────────┘
            │ updates
            ▼
   ┌──────────────────────────┐
   │  EvaluationContext       │
   │   Variables (Dict)       │
   │   ExecutedActions (List) │
   └──────────────────────────┘
            │ projects to
            ▼
   ┌──────────────────────────┐
   │ AstRulesEvaluationResult │
   │ Success / Output / Audit │
   └──────────────────────────┘
```

The visitor is a single class — adding a new node type means adding
both a record (in `AstNodes.cs`) and one method on
`IAstVisitor<T>`.

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

The DSL has four moving parts:

| Section          | Required? | Contents                                                              |
|------------------|-----------|-----------------------------------------------------------------------|
| `ruleName`       | optional  | Author-friendly name; defaults to `"unnamed"`                         |
| `version`        | optional  | Free-form version string; defaults to `"1"`                           |
| `inputVariables` | optional  | Map of `name: type` pairs the rule expects (used for validation only) |
| `conditions`     | optional  | List of conditions joined by **AND** (all must be true)               |
| `actions`        | optional  | List of actions executed in order when conditions all pass            |

Conditions can be plain strings (`$a > 5`) or nested operators
(`and: [...]`, `or: [...]`, `xor: [...]`). The plain-string form is
parsed as `<variable> <op> <literal>` — three whitespace-separated
tokens.

### Supported comparison operators

| Spelling                          | Meaning                                |
|-----------------------------------|----------------------------------------|
| `==`, `=`, `eq`                   | equal                                  |
| `!=`, `<>`, `ne`                  | not equal                              |
| `<`, `lt`                         | less than                              |
| `<=`, `le`                        | less than or equal                     |
| `>`, `gt`                         | greater than                           |
| `>=`, `ge`                        | greater than or equal                  |
| `contains`                        | string contains substring              |
| `startsWith`                      | string starts with substring           |
| `endsWith`                        | string ends with substring             |

### Supported assignment operators

| Spelling | Meaning           |
|----------|-------------------|
| `=`      | assign            |
| `+=`     | numeric or string concatenation |
| `-=`     | numeric subtract  |
| `*=`     | numeric multiply  |
| `/=`     | numeric divide    |

### Variable references

A leading `$` distinguishes a variable from a string literal:
`$amount` is a variable lookup, `"amount"` is a literal. Numeric
literals are parsed via invariant culture (`decimal.TryParse` with
`NumberStyles.Number`).

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

// result.Success                    -> true
// result.VariableValues["discount"] -> 0.15m
// result.VariableValues["tier"]     -> "gold"
// result.ExecutedActions             -> ["assign discount", "assign tier"]
```

## Public surface

### AST nodes

```
Expression                   Condition                   Action
├─ BinaryExpression          ├─ ComparisonCondition      ├─ AssignmentAction
├─ UnaryExpression           ├─ LogicalCondition         ├─ FunctionCallAction
├─ VariableExpression        └─ ExpressionCondition      ├─ ConditionalAction
├─ LiteralExpression                                     ├─ ForEachAction
├─ FunctionCallExpression                                └─ WhileAction
└─ ArithmeticExpression
```

Every node derives from `AstNode` and implements
`Accept<T>(IAstVisitor<T>)` for the visitor pattern. The visitor is
*the* extension point: write your own `IAstVisitor<T>` to traverse
rules for analysis (e.g. extract referenced variables, render to
Mermaid, or transpile to another DSL).

### Visitor

```csharp
public interface IAstVisitor<out T>
{
    T VisitBinary(BinaryExpression node);
    T VisitUnary(UnaryExpression node);
    T VisitVariable(VariableExpression node);
    T VisitLiteral(LiteralExpression node);
    T VisitFunctionCall(FunctionCallExpression node);
    T VisitArithmetic(ArithmeticExpression node);
    T VisitComparison(ComparisonCondition node);
    T VisitLogical(LogicalCondition node);
    T VisitExpressionCondition(ExpressionCondition node);
    T VisitAssignment(AssignmentAction node);
    T VisitFunctionCallAction(FunctionCallAction node);
    T VisitConditional(ConditionalAction node);
    T VisitForEach(ForEachAction node);
    T VisitWhile(WhileAction node);
}
```

### Parser + evaluator

| Type                         | Purpose                                                          |
|------------------------------|------------------------------------------------------------------|
| `YamlDslParser`              | YamlDotNet-driven; produces `AstRulesDsl`                        |
| `EvaluationContext`          | Mutable variable map + list of executed actions                  |
| `AstRulesEvaluationEngine`   | `IAstVisitor<object?>` implementation; the canonical evaluator   |
| `AstRulesEvaluationResult`   | Output record with `Success`, `Output`, `ExecutedActions`, `VariableValues`, `ErrorMessage` |

### Service layer

| Interface                       | Purpose                                                     |
|---------------------------------|-------------------------------------------------------------|
| `IRulesEvaluationService`       | Inline / by-code / batch evaluation                         |
| `IBatchRulesEvaluationService`  | High-throughput parallel evaluator                          |
| `IRuleDefinitionService`        | CRUD over `RuleDefinitionEntity`                            |
| `IConstantService`              | CRUD over `ConstantEntity`                                  |
| `IAuditTrailService`            | Read / filter `AuditTrailEntity`                            |

`RulesEvaluationService` exposes three entry points:

- `EvaluateRulesDirectAsync(base64YamlContent, input)` — the rule body
  is supplied inline as base-64-encoded YAML.
- `EvaluateRulesPlainAsync(plainYaml, input)` — same, with plain-text
  YAML.
- `EvaluateRuleByCodeAsync(ruleCode, input)` — looks the rule up via
  `IRuleDefinitionService`.

### Result shape

```csharp
public sealed record AstRulesEvaluationResult(
    bool                         Success,
    Dictionary<string, object?>  Output,
    List<string>                 ExecutedActions,
    Dictionary<string, object?>  VariableValues,
    string?                      ErrorMessage = null);
```

`Output` and `VariableValues` are deliberately separate copies so
callers can distinguish "every variable touched" from "the final
state of every variable" if they care about the order of operations.
For most use cases they're the same map.

## Common patterns

### Wiring the service

```csharp
services.AddSingleton<IRulesEvaluationService, RulesEvaluationService>();
services.AddSingleton<IRuleDefinitionService, RuleDefinitionService>();
services.AddSingleton<IConstantService, ConstantService>();
services.AddSingleton<IAuditTrailService, AuditTrailService>();
```

The default `RulesEvaluationService` is stateless — it only needs the
`IRuleDefinitionService` to resolve `EvaluateRuleByCodeAsync` and an
`IAuditTrailService` to log decisions.

### Evaluating with a constant

If a rule references a constant `$minVipAmount`, the evaluator
resolves it via the supplied input dictionary first, then via
`IConstantService`. Resolve constants once and merge into the input:

```csharp
var constants = await constantService.GetAllAsync(ct);
var input = new Dictionary<string, object?>
{
    ["amount"] = amount,
    ["isVip"]  = isVip,
};
foreach (var c in constants) input.TryAdd(c.Key, Coerce(c.Value, c.DataType));

var result = await rules.EvaluateRuleByCodeAsync(
    new RuleEvaluationByCodeRequestDto("vip-discount", input), ct);
```

### Building a custom visitor

A common analytics task is "which input variables does this rule
read?" — write a visitor that returns a `HashSet<string>`:

```csharp
public sealed class ReadVariablesVisitor : IAstVisitor<HashSet<string>>
{
    public HashSet<string> VisitVariable(VariableExpression node) => [node.Name];
    public HashSet<string> VisitLiteral(LiteralExpression _) => [];
    public HashSet<string> VisitBinary(BinaryExpression node) =>
        node.Left.Accept(this).Concat(node.Right.Accept(this)).ToHashSet();
    public HashSet<string> VisitComparison(ComparisonCondition node) =>
        node.Left.Accept(this).Concat(node.Right.Accept(this)).ToHashSet();
    // ... fill in the rest by delegating to children
    public HashSet<string> VisitAssignment(AssignmentAction node) =>
        new() { node.Variable, ..node.Value.Accept(this) };
    public HashSet<string> VisitConditional(ConditionalAction node) =>
        node.Condition.Accept(this)
            .Concat(node.ThenBranch.SelectMany(a => a.Accept(this)))
            .Concat(node.ElseBranch.SelectMany(a => a.Accept(this)))
            .ToHashSet();
    // ... etc
    public HashSet<string> VisitUnary(UnaryExpression n) => n.Operand.Accept(this);
    public HashSet<string> VisitFunctionCall(FunctionCallExpression n) =>
        n.Arguments.SelectMany(a => a.Accept(this)).ToHashSet();
    public HashSet<string> VisitArithmetic(ArithmeticExpression n) =>
        n.Left.Accept(this).Concat(n.Right.Accept(this)).ToHashSet();
    public HashSet<string> VisitLogical(LogicalCondition n) =>
        n.Left.Accept(this).Concat(n.Right.Accept(this)).ToHashSet();
    public HashSet<string> VisitExpressionCondition(ExpressionCondition n) => n.Expression.Accept(this);
    public HashSet<string> VisitFunctionCallAction(FunctionCallAction n) =>
        n.Arguments.SelectMany(a => a.Accept(this)).ToHashSet();
    public HashSet<string> VisitForEach(ForEachAction n) =>
        n.Collection.Accept(this).Concat(n.Body.SelectMany(a => a.Accept(this))).ToHashSet();
    public HashSet<string> VisitWhile(WhileAction n) =>
        n.Condition.Accept(this).Concat(n.Body.SelectMany(a => a.Accept(this))).ToHashSet();
}

var inputs = ast.Conditions.SelectMany(c => c.Accept(visitor))
                           .Concat(ast.Actions.SelectMany(a => a.Accept(visitor)))
                           .ToHashSet();
```

This visitor finds every variable a rule touches without running the
rule.

## Pitfalls and gotchas

- **`while` loops cap at 10,000 iterations.** The evaluator throws
  `InvalidOperationException("Loop limit reached")` to prevent
  malicious or buggy rules from hanging the host. Adjust by extending
  the engine — there's no config knob for it on purpose.
- **`null` is falsy, `0` is falsy, everything else is truthy.** This
  matches the JavaScript-style coercion the Java line uses.
- **Numeric arithmetic uses `decimal`.** Floats are coerced to
  `decimal` on the way in, so `0.1 + 0.2 == 0.3` (no IEEE 754 surprise).
  But conversions cost — for high-throughput numeric rules use
  literals that are already decimal.
- **`+` on a string concatenates.** If the left operand is a string,
  `+` produces a string regardless of the right type. Order matters:
  `1 + "x"` is `"1x"` only if the parser placed the string on the
  left.
- **The plain-string condition format is rigid.** `$x > 5` works;
  `$x>5` does not (no whitespace) and `$x > 5 + 1` does not
  (more than three tokens). Use the nested object form for richer
  expressions.
- **`ExecutedActions` records action *names*, not their effects.** It
  reports `"assign discount"` rather than the new value. The new
  value is in `VariableValues`.

## Internals (for the curious)

- `EvaluationContext.Variables` is a plain `Dictionary<string, object?>`.
  Evaluation is single-threaded by design; concurrent evaluation
  uses one engine per request.
- The `ToDecimal` helper accepts strings (parsed as
  `NumberStyles.Number` in invariant culture) so input data can come
  from JSON without pre-coercion. Failures yield `0`, not a thrown
  exception, to match the Java line.
- The visitor is double-dispatched via `Accept<T>` on each AST node.
  Adding a new node type requires adding both the record and a method
  on `IAstVisitor<T>`; the compiler enforces every visitor handles
  every node.
- `AstRulesEvaluationEngine` catches every exception and reports it
  via `AstRulesEvaluationResult.ErrorMessage`. This is intentional:
  rule failures should not crash the host. The control plane decides
  whether a `Success = false` result fails the request.

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
| `AstRulesDsl`                 | `RulesDSL`                          |
| `AstRulesEvaluationResult`    | `RulesEvaluationResult`             |

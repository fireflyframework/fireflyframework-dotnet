# FireflyFramework.RuleEngine.Interfaces

## Overview

`FireflyFramework.RuleEngine.Interfaces` is the **public contract
module** for the Firefly rule-engine subsystem. It is a tiny,
dependency-free assembly that ships only DTOs (record types) and
enums describing the wire format of the rule-engine REST API and the
shape of the audit trail.

In Firefly's hub-and-spoke architecture, the *interfaces* tier of every
multi-project service plays the same role: it is the only assembly
that external callers reference. Other services that want to call the
rule engine over HTTP — directly or via the typed
`RuleEngine.Sdk` — pull this assembly in to deserialize responses and
craft requests, but never load `RuleEngine.Core` (which carries the
parser, AST visitor, and YAML processing).

Mirrors `org.fireflyframework:firefly-common-rule-engine-interfaces`
on the Java side. The DTO names are intentionally one-to-one with the
Java records, with the only superficial change being the `Dto` suffix
instead of the Java `DTO`.

## Why a separate module?

The DSL parser (YamlDotNet) and the visitor evaluator together pull
in ~1 MB of indirect dependencies. A consumer that only wants to
*invoke* a rule (e.g. an order service that asks the rule engine for
a discount tier) shouldn't pay that cost. By putting only the wire
shapes in `Interfaces`, the consumer takes a 30 KB, dependency-free
assembly and the framework stays composable.

## Mental model

```
                         consumer service
                                │
                                ▼
                  ┌─────────────────────────────┐
                  │ RuleEngine.Sdk              │
                  │ typed HttpClient            │
                  └──────────────┬──────────────┘
                                 │ uses DTOs from
                                 ▼
                  ┌─────────────────────────────┐
                  │ RuleEngine.Interfaces       │  ← this module
                  │   - RuleDefinitionDto        │
                  │   - RulesEvaluationRequestDto│
                  │   - RulesEvaluationResponseDto│
                  │   - AuditTrailDto            │
                  │   - enums                    │
                  └──────────────▲──────────────┘
                                 │ used by
                                 │
   ┌─────────────────────────────┼─────────────────────────────┐
   │                             │                             │
   ▼                             ▼                             ▼
┌─────────────┐          ┌──────────────┐            ┌─────────────────┐
│ Models      │          │ Core         │            │ Web             │
│ (EF Core)   │          │ (parser/eval)│            │ (REST endpoint) │
└─────────────┘          └──────────────┘            └─────────────────┘
```

The compiler enforces the dependency direction: every `csproj` only
references the tier directly below it, and `Interfaces` has no
project references at all.

## Public surface

### Rule DTOs

```csharp
public sealed record RuleDefinitionDto(
    Guid?    Id,
    string   Code,
    string   Name,
    string?  Description,
    string   YamlContent,
    string   Version,
    bool     IsActive,
    string[]? ValidationErrors = null);

public sealed record ConstantDto(
    Guid?         Id,
    string        Key,
    string        Value,
    string?       Description,
    RuleValueType DataType);
```

| Field on `RuleDefinitionDto` | Notes                                                          |
|------------------------------|----------------------------------------------------------------|
| `Id`                         | Nullable on the wire — clients POST without an id              |
| `Code`                       | Stable identifier consumers use for `EvaluateRuleByCodeAsync`  |
| `YamlContent`                | The rule body (DSL) as plain YAML text                         |
| `Version`                    | Author-controlled; used for audit and migration                |
| `IsActive`                   | Soft-disable a rule without deleting it                        |
| `ValidationErrors`           | Populated by the validation endpoint; null for a saved rule    |

### Evaluation request DTOs

| DTO                                  | Body                                                   |
|--------------------------------------|--------------------------------------------------------|
| `RulesEvaluationRequestDto`          | `{ Base64YamlContent, InputData }`                     |
| `PlainYamlEvaluationRequestDto`      | `{ YamlContent, InputData }`                           |
| `RuleEvaluationByCodeRequestDto`     | `{ RuleCode, InputData }`                              |
| `BatchRulesEvaluationRequestDto`     | `{ Evaluations[], ConcurrencyLimit, TimeoutMs }`       |

The base-64 variant exists because some HTTP toolchains mangle YAML
indentation when passing it through query strings or URL-encoded
forms. Use the plain variant when posting JSON.

### Evaluation response DTOs

```csharp
public sealed record RulesEvaluationResponseDto(
    bool                            Success,
    Dictionary<string, object?>     Output,
    long                            ExecutionTimeMs,
    string?                         RuleCode,
    Guid?                           AuditId,
    string?                         ErrorMessage = null);

public sealed record BatchRulesEvaluationResponseDto(
    List<RulesEvaluationResponseDto> Results,
    int  TotalCount,
    int  SuccessCount,
    int  FailureCount,
    long TotalTimeMs,
    int  CacheHitCount);
```

`Output` carries every variable that was set or modified during
evaluation, keyed on the variable name (without the `$` prefix used
in the DSL).

### Audit trail DTOs

```csharp
public sealed record AuditTrailDto(
    Guid                              Id,
    AuditEventType                    OperationType,
    string                            EntityType,
    string?                           EntityId,
    string?                           RuleCode,
    string?                           UserId,
    string?                           IpAddress,
    string?                           UserAgent,
    string?                           HttpMethod,
    string?                           Endpoint,
    string?                           RequestData,
    string?                           ResponseData,
    int?                              StatusCode,
    bool                              Success,
    string?                           ErrorMessage,
    long?                             ExecutionTimeMs,
    Dictionary<string, object?>?      Metadata,
    string?                           SessionId,
    string?                           CorrelationId,
    DateTimeOffset                    CreatedAt);

public sealed record AuditTrailFilterDto(
    string?           RuleCode,
    string?           UserId,
    AuditEventType?   OperationType,
    DateTimeOffset?   StartDate,
    DateTimeOffset?   EndDate,
    int               Limit = 100);
```

`AuditTrailDto` is deliberately rich because the rule engine often
sits at compliance-critical decision points (lending decisions,
fraud scoring, KYC). The schema captures everything an auditor will
ask for: what input went in, what output came out, who triggered it,
when, and from where.

### Enums

| Enum             | Values                                                                                  |
|------------------|-----------------------------------------------------------------------------------------|
| `RuleValueType`  | `String`, `Number`, `Boolean`, `Json`                                                    |
| `ResultType`     | `Success`, `Failure`, `Warning`                                                          |
| `AuditEventType` | `RuleDefinitionCreate`, `RuleDefinitionUpdate`, `RuleDefinitionDelete`, `RuleEvaluationDirect`, `RuleEvaluationByCode`, `RuleEvaluationPlain` |

### Validation DTOs

```csharp
public sealed record ValidateYamlRequest(string YamlContent);
public sealed record ValidationResult(bool IsValid, List<string> Errors);
```

Used by the optional `POST /api/rules/validate` endpoint to lint a
rule body without persisting it.

## Common patterns

### Building an evaluation request

```csharp
var request = new RulesEvaluationRequestDto(
    Base64YamlContent: Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml)),
    InputData:         new Dictionary<string, object?>
    {
        ["amount"] = 1500m,
        ["isVip"]  = true,
    });
```

### Inspecting an evaluation response

```csharp
if (!response.Success)
{
    log.LogWarning("Rule {Code} evaluation failed in {Ms} ms: {Err}",
        response.RuleCode, response.ExecutionTimeMs, response.ErrorMessage);
    return Defaults.NoDiscount();
}

var discount = response.Output.TryGetValue("discount", out var d) && d is decimal v
    ? v
    : 0m;
```

### Filtering audit trails

```csharp
var filter = new AuditTrailFilterDto(
    RuleCode:      "vip-discount",
    UserId:        null,
    OperationType: AuditEventType.RuleEvaluationByCode,
    StartDate:     DateTimeOffset.UtcNow.AddDays(-30),
    EndDate:       DateTimeOffset.UtcNow,
    Limit:         500);
```

## Pitfalls and gotchas

- **Records are value-equal.** Two `RulesEvaluationRequestDto`
  instances with identical fields compare equal — convenient for
  caching, but mutate-via-`with { … }` returns a fresh reference.
- **`InputData` values are `object?`.** That's because the rule
  engine accepts heterogeneous values (strings, numbers, booleans,
  JSON sub-trees). On the server side, numeric values are normalised
  to `decimal` before evaluation.
- **`AuditTrailDto.Metadata` is provider-defined.** The framework
  carries it through but doesn't constrain the schema. Document the
  metadata keys per environment.
- **`BatchRulesEvaluationRequestDto.ConcurrencyLimit` is advisory.**
  The server enforces its own ceiling. A request with
  `ConcurrencyLimit = 1000` may run with effective concurrency 8 if
  the server is configured that way.
- **Time-stamps are `DateTimeOffset`.** Always serialize and
  round-trip in UTC; the .NET `JsonSerializer` defaults to ISO-8601
  with offset.

## Internals (for the curious)

- These records compile down to immutable C# classes with synthesized
  `Equals`, `GetHashCode`, and `Deconstruct`. JSON serialisation via
  `System.Text.Json` round-trips without any extra attributes — the
  property names match the parameter names.
- The choice to keep these as `record` types rather than mutable
  POCOs is deliberate: DTOs that flow over the wire should be
  immutable; mutation is reserved for the `Models` tier (which models
  EF Core entities).

## Dependencies

None — pure DTOs.

## Java mapping

| .NET                                | Java                            |
|-------------------------------------|---------------------------------|
| `RuleDefinitionDto`                 | `RuleDefinitionDTO`             |
| `ConstantDto`                       | `ConstantDTO`                   |
| `AuditTrailDto`                     | `AuditTrailDTO`                 |
| `AuditTrailFilterDto`               | `AuditTrailFilterDTO`           |
| `RulesEvaluationRequestDto`         | `RulesEvaluationRequestDTO`     |
| `PlainYamlEvaluationRequestDto`     | `PlainYamlEvaluationRequestDTO` |
| `RuleEvaluationByCodeRequestDto`    | `RuleEvaluationByCodeRequestDTO`|
| `RulesEvaluationResponseDto`        | `RulesEvaluationResponseDTO`    |
| `BatchRulesEvaluationRequestDto`    | `BatchRulesEvaluationRequestDTO`|
| `BatchRulesEvaluationResponseDto`   | `BatchRulesEvaluationResponseDTO`|
| `AuditEventType`                    | `AuditEventType`                |
| `RuleValueType`                     | `RuleValueType`                 |
| `ResultType`                        | `ResultType`                    |

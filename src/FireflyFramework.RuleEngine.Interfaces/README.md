# FireflyFramework.RuleEngine.Interfaces

Public contract for the rule engine. Pure DTOs and enums with no
implementation dependencies, designed for consumption from REST clients
that want type-safe DTOs without pulling the full evaluation engine.

Mirrors `org.fireflyframework:firefly-common-rule-engine-interfaces`.

## Public surface

| Type                       | Purpose                                                                |
|----------------------------|------------------------------------------------------------------------|
| `RuleDefinitionDto`        | Persistable rule definition: `Id`, `Code`, `Name`, `YamlContent`, `Version`, `IsActive`, `ValidationErrors` |
| `ConstantDto`              | Named constant available to rule expressions: `Key`, `Value`, `DataType` |
| `RuleValueType`            | `String`, `Number`, `Boolean`, `Json`                                  |
| `ResultType`               | `Success`, `Failure`, `Warning`                                         |
| `AuditTrailDto`            | Per-operation audit record (operation type, user, http method, request / response data, status code, execution time, correlation id) |
| `AuditTrailFilterDto`      | Filter criteria for listing audit trails                                |
| `AuditEventType`           | `RuleDefinitionCreate`, `RuleDefinitionUpdate`, `RuleDefinitionDelete`, `RuleEvaluationDirect`, `RuleEvaluationByCode`, `RuleEvaluationPlain` |

## Dependencies

None — pure DTOs.

## Java mapping

| .NET                    | Java                                                                    |
|-------------------------|-------------------------------------------------------------------------|
| `RuleDefinitionDto`     | `RuleDefinitionDTO`                                                     |
| `ConstantDto`           | `ConstantDTO`                                                           |
| `AuditTrailDto`         | `AuditTrailDTO`                                                         |
| `AuditEventType`        | `AuditEventType`                                                        |

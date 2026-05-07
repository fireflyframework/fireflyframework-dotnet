# FireflyFramework.RuleEngine.Models

Entity-Framework-Core persistence model for the rule engine. Sits
between `Interfaces` (DTOs) and `Core` (evaluator) so application code
can store rules and audit trails without pulling the evaluator.

Mirrors `org.fireflyframework:firefly-common-rule-engine-models`.

## Public surface

| Entity                    | Maps to                                                          |
|---------------------------|------------------------------------------------------------------|
| `RuleDefinitionEntity`    | `firefly_rule_definitions` — id, code, name, description, yaml_content, version, is_active, created/updated metadata |
| `ConstantEntity`          | `firefly_rule_constants`   — key, value, data_type, description  |
| `AuditTrailEntity`        | `firefly_rule_audit_trails` — full audit row (see `AuditTrailDto` shape in `Interfaces`) |

Every entity inherits from `FireflyFramework.Data.BaseEntity<Guid>` so
it picks up the standard `Id` contract.

## Dependencies

| Reference                        | Used for                |
|----------------------------------|-------------------------|
| `FireflyFramework.Data`          | `BaseEntity<TId>`       |
| `FireflyFramework.RuleEngine.Interfaces` | DTO ↔ Entity mapping |

## Java mapping

| .NET                       | Java                              |
|----------------------------|-----------------------------------|
| `RuleDefinitionEntity`     | `RuleDefinition`                  |
| `ConstantEntity`           | `Constant`                        |
| `AuditTrailEntity`         | `AuditTrail`                      |

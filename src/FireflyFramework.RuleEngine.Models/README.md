# FireflyFramework.RuleEngine.Models

## Overview

`FireflyFramework.RuleEngine.Models` is the **persistence-tier
companion** to `FireflyFramework.RuleEngine.Interfaces`. It defines
the EF Core entity classes that Firefly's reference rule-engine
deployment uses to store rule definitions, constants, and audit
trails. Mirrors `org.fireflyframework:firefly-common-rule-engine-models`
(JPA entities) on the Java side.

The split between `Interfaces` (DTOs) and `Models` (entities) is
deliberate: a service that simply *consumes* the rule engine over
HTTP needs the DTOs but not the entity model, and a custom data store
implementation can swap entities without altering the wire format.

## Why a separate module?

In Firefly's hub-and-spoke convention every multi-project subsystem
follows the same five-tier shape:

```
Interfaces ◄── Models ◄── Core ◄── Web
                            ▲
                            │
                    Sdk ────┘ (referencing Interfaces only)
```

The compiler enforces the dependency direction. `Models` references
`Interfaces` (for the enum types it stores) and `FireflyFramework.Data`
(for `BaseEntity<TId>`); nothing references `Models` *from above*
except `Core` and the host service that wires its DbContext. This
keeps your service's data-access concerns in one assembly and the
service's wire format in another.

## Mental model

```
   ┌──────────────────────────────┐
   │  RuleEngine.Interfaces       │  DTOs (RuleDefinitionDto, …)
   └──────────────┬───────────────┘
                  │ (DataType: RuleValueType)
                  │ (OperationType: AuditEventType)
                  ▼
   ┌──────────────────────────────┐
   │  RuleEngine.Models           │  ← this module
   │   ┌─────────────────────┐    │
   │   │ RuleDefinitionEntity │   │
   │   │ ConstantEntity       │   │
   │   │ AuditTrailEntity     │   │
   │   └─────────────────────┘    │
   │     all derive from          │
   │     BaseEntity<Guid>          │
   └──────────────────────────────┘
                  │ stored by
                  ▼
   ┌──────────────────────────────┐
   │ EF Core DbContext supplied   │
   │ by your service              │
   └──────────────────────────────┘
```

The framework does not ship a DbContext or migrations. Your service
adds these entities to its own DbContext, runs your own migration
tool, and the rule-engine `Core` services use `IRuleDefinitionStore`
or `IConstantStore` to read/write through that DbContext.

## Public surface

### `RuleDefinitionEntity`

```csharp
public sealed class RuleDefinitionEntity : BaseEntity<Guid>
{
    public string  Code        { get; set; } = string.Empty;
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string  YamlContent { get; set; } = string.Empty;
    public string  Version     { get; set; } = "1";
    public bool    IsActive    { get; set; } = true;
    public string? Tags        { get; set; }
}
```

| Column         | Type        | Notes                                                  |
|----------------|-------------|--------------------------------------------------------|
| `Id`           | `Guid`      | Inherited from `BaseEntity<Guid>`                      |
| `Code`         | `string`    | Unique stable identifier consumers look up rules by    |
| `Name`         | `string`    | Human-readable                                         |
| `Description`  | `string?`   | Optional one-liner                                     |
| `YamlContent`  | `string`    | The full DSL body                                       |
| `Version`      | `string`    | Author-controlled; not auto-incremented                |
| `IsActive`     | `bool`      | Soft-disable flag                                      |
| `Tags`         | `string?`   | Free-form, typically comma-separated (`"lending,fraud"`) |

The recommended physical schema (Postgres):

```sql
CREATE TABLE firefly_rule_definitions (
    id           UUID PRIMARY KEY,
    code         TEXT NOT NULL UNIQUE,
    name         TEXT NOT NULL,
    description  TEXT,
    yaml_content TEXT NOT NULL,
    version      TEXT NOT NULL DEFAULT '1',
    is_active    BOOLEAN NOT NULL DEFAULT TRUE,
    tags         TEXT
);
CREATE INDEX ix_firefly_rules_active ON firefly_rule_definitions (is_active) WHERE is_active;
CREATE INDEX ix_firefly_rules_tags   ON firefly_rule_definitions USING gin (string_to_array(tags, ','));
```

### `ConstantEntity`

```csharp
public sealed class ConstantEntity : BaseEntity<Guid>
{
    public string         Key         { get; set; } = string.Empty;
    public string         Value       { get; set; } = string.Empty;
    public string?        Description { get; set; }
    public RuleValueType  DataType    { get; set; } = RuleValueType.String;
}
```

Constants are named values rules can refer to without re-defining
them per rule:

```yaml
# rule body
conditions:
  - $amount > $minVipAmount         # $minVipAmount comes from ConstantEntity
```

| Column        | Type             | Notes                                       |
|---------------|------------------|---------------------------------------------|
| `Key`         | `string`         | The variable name (without the `$`)         |
| `Value`       | `string`         | Stored as text; coerced per `DataType`      |
| `DataType`    | `RuleValueType`  | `String` / `Number` / `Boolean` / `Json`     |
| `Description` | `string?`        | Optional documentation                      |

### `AuditTrailEntity`

```csharp
public sealed class AuditTrailEntity : BaseEntity<Guid>
{
    public AuditEventType OperationType   { get; set; }
    public string         EntityType      { get; set; } = string.Empty;
    public string?        EntityId        { get; set; }
    public string?        RuleCode        { get; set; }
    public string?        UserId          { get; set; }
    public string?        IpAddress       { get; set; }
    public string?        UserAgent       { get; set; }
    public string?        HttpMethod      { get; set; }
    public string?        Endpoint        { get; set; }
    public string?        RequestData     { get; set; }
    public string?        ResponseData    { get; set; }
    public int?           StatusCode      { get; set; }
    public bool           Success         { get; set; }
    public string?        ErrorMessage    { get; set; }
    public long?          ExecutionTimeMs { get; set; }
    public string?        Metadata        { get; set; }    // JSON-serialised
    public string?        SessionId       { get; set; }
    public string?        CorrelationId   { get; set; }
}
```

Note that `Metadata` is stored as a JSON string column, not a typed
dictionary. The DTO unmarshals it on read; the EF Core mapping doesn't
need a value converter unless you want to project the column.

For audit-heavy deployments, partition this table by month:

```sql
CREATE TABLE firefly_rule_audit_trails (
    id                UUID PRIMARY KEY,
    operation_type    TEXT NOT NULL,
    entity_type       TEXT NOT NULL,
    rule_code         TEXT,
    user_id           TEXT,
    success           BOOLEAN NOT NULL,
    execution_time_ms BIGINT,
    correlation_id    TEXT,
    request_data      TEXT,
    response_data     TEXT,
    metadata          JSONB,
    created_at        TIMESTAMPTZ NOT NULL,
    -- ... others
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);
```

## Common patterns

### Wiring into your DbContext

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> opts) : DbContext(opts)
{
    public DbSet<RuleDefinitionEntity> RuleDefinitions => Set<RuleDefinitionEntity>();
    public DbSet<ConstantEntity>       Constants       => Set<ConstantEntity>();
    public DbSet<AuditTrailEntity>     AuditTrails     => Set<AuditTrailEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RuleDefinitionEntity>(e =>
        {
            e.ToTable("firefly_rule_definitions");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.YamlContent).HasMaxLength(64_000);
        });

        b.Entity<ConstantEntity>(e =>
        {
            e.ToTable("firefly_rule_constants");
            e.HasIndex(x => x.Key).IsUnique();
        });

        b.Entity<AuditTrailEntity>(e =>
        {
            e.ToTable("firefly_rule_audit_trails");
            e.HasIndex(x => new { x.RuleCode, x.OperationType });
            e.HasIndex(x => x.CorrelationId);
        });
    }
}
```

### DTO ↔ entity mapping

Mapping is straightforward because field names match between DTOs
and entities; a tiny mapper or AutoMapper profile suffices:

```csharp
public static RuleDefinitionDto ToDto(this RuleDefinitionEntity e) =>
    new(e.Id, e.Code, e.Name, e.Description, e.YamlContent, e.Version, e.IsActive);

public static RuleDefinitionEntity ToEntity(this RuleDefinitionDto d) => new()
{
    Code = d.Code,
    Name = d.Name,
    Description = d.Description,
    YamlContent = d.YamlContent,
    Version = d.Version,
    IsActive = d.IsActive,
};
```

The framework's `Core` tier exposes `IRuleDefinitionStore` /
`IConstantStore` SPIs — implement them against your DbContext to
wire the engine to a real database.

## Pitfalls and gotchas

- **`Code` should be unique.** The lookup-by-code endpoint
  (`POST /api/rules/evaluate/by-code`) returns the first match. Add a
  unique index in your migration.
- **`YamlContent` can be large.** A complex rule with many constants
  can run to several KB. Pick `text` (Postgres) / `nvarchar(max)` (SQL
  Server) — don't use a fixed-width column.
- **Tags are free-form.** The framework doesn't validate them. If you
  need controlled vocabulary, add a separate `RuleTagEntity` lookup
  table.
- **`Metadata` is JSON-serialised.** Use `JsonSerializer` on read; the
  framework doesn't auto-deserialise on the entity side because
  different deployments want different columns.
- **`AuditTrailEntity` grows fast.** Plan retention from day one.
  Partition the table or add a `DELETE FROM ... WHERE created_at <
  NOW() - INTERVAL '90 days'` cron.

## Internals (for the curious)

- All three entities derive from
  `FireflyFramework.Data.Domain.BaseEntity<Guid>` so they pick up the
  `Id` contract uniformly with the rest of the framework.
- The entities deliberately use `string` for `Tags` and JSON-string
  for `Metadata` rather than collections, because EF Core 10's
  `JSON_TYPE` mapping varies by provider. Storing as `string` keeps
  the mapping provider-agnostic; you can opt-in to typed JSON columns
  per provider in your `OnModelCreating` if you want.

## Dependencies

| Reference                                | Used for                |
|------------------------------------------|-------------------------|
| `FireflyFramework.Data`                  | `BaseEntity<TId>`       |
| `FireflyFramework.RuleEngine.Interfaces` | DTO ↔ Entity mapping    |

## Java mapping

| .NET                       | Java                              |
|----------------------------|-----------------------------------|
| `RuleDefinitionEntity`     | `RuleDefinition`                  |
| `ConstantEntity`           | `Constant`                        |
| `AuditTrailEntity`         | `AuditTrail`                      |

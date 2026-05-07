namespace FireflyFramework.Data.Domain;

/// <summary>Marker interface for entities with a typed identifier. Mirrors Java <c>BaseEntity</c>.</summary>
public interface IBaseEntity<out TId>
{
    TId Id { get; }
}

/// <summary>Adds optimistic concurrency token (RowVersion) and audit timestamps.</summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}

/// <summary>Marker for entities that participate in optimistic concurrency.</summary>
public interface IVersionedEntity
{
    /// <summary>Optimistic concurrency token. Mapped to RowVersion / xmin in EF Core.</summary>
    byte[]? RowVersion { get; set; }
}

/// <summary>Marker for soft-delete support.</summary>
public interface ISoftDeleteEntity
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>Marker for tenant scoping.</summary>
public interface ITenantScopedEntity
{
    string TenantId { get; set; }
}

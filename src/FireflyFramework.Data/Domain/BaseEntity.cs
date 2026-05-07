namespace FireflyFramework.Data.Domain;

/// <summary>Convenience base for entities with a typed Id, audit columns and a row version.</summary>
public abstract class BaseEntity<TId> : IBaseEntity<TId>, IAuditableEntity, IVersionedEntity
{
    public TId Id { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[]? RowVersion { get; set; }
}

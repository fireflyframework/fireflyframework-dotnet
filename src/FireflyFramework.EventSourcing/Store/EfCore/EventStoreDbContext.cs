using Microsoft.EntityFrameworkCore;

namespace FireflyFramework.EventSourcing.Store.EfCore;

/// <summary>
/// EF Core <see cref="DbContext"/> for the persistent event store. Mirrors the Java
/// R2DBC schema: append-only events table + outbox + snapshots.
/// </summary>
public sealed class EventStoreDbContext : DbContext
{
    public EventStoreDbContext(DbContextOptions<EventStoreDbContext> options) : base(options) { }

    public DbSet<EventEntity> Events => Set<EventEntity>();
    public DbSet<SnapshotEntity> Snapshots => Set<SnapshotEntity>();
    public DbSet<EventOutboxEntity> Outbox => Set<EventOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<EventEntity>(b =>
        {
            b.ToTable("firefly_events");
            b.HasKey(e => e.GlobalSequence);
            b.Property(e => e.GlobalSequence).ValueGeneratedOnAdd();
            b.HasIndex(e => new { e.AggregateType, e.AggregateId, e.AggregateVersion }).IsUnique();
            b.HasIndex(e => e.GlobalSequence);
            b.Property(e => e.AggregateId).IsRequired();
            b.Property(e => e.AggregateType).HasMaxLength(255).IsRequired();
            b.Property(e => e.EventType).HasMaxLength(255).IsRequired();
            b.Property(e => e.Payload).IsRequired();
        });

        builder.Entity<SnapshotEntity>(b =>
        {
            b.ToTable("firefly_snapshots");
            b.HasKey(s => s.Id);
            b.HasIndex(s => new { s.AggregateId, s.SnapshotType, s.AggregateVersion });
            b.Property(s => s.SnapshotType).HasMaxLength(255).IsRequired();
            b.Property(s => s.Payload).IsRequired();
        });

        builder.Entity<EventOutboxEntity>(b =>
        {
            b.ToTable("firefly_event_outbox");
            b.HasKey(o => o.Id);
            b.HasIndex(o => new { o.Published, o.CreatedAt });
        });
    }
}

public sealed class EventEntity
{
    public long GlobalSequence { get; set; }
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int EventVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string? HeadersJson { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? TenantId { get; set; }
}

public sealed class SnapshotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AggregateId { get; set; }
    public string SnapshotType { get; set; } = string.Empty;
    public long AggregateVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? TenantId { get; set; }
}

public sealed class EventOutboxEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long GlobalSequence { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Published { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
}

namespace FireflyFramework.Cqrs.Annotations;

/// <summary>Marks a class as a command handler with optional metadata. Mirrors <c>@CommandHandlerComponent</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CommandHandlerComponentAttribute : Attribute
{
    public int TimeoutMs { get; set; } = 30_000;
    public int Retries { get; set; }
    public int BackoffMs { get; set; } = 100;
    public bool Metrics { get; set; } = true;
    public bool Tracing { get; set; } = true;
    public bool Validation { get; set; } = true;
    public int Priority { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string? Description { get; set; }
}

/// <summary>Marks a class as a query handler with optional metadata. Mirrors <c>@QueryHandlerComponent</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class QueryHandlerComponentAttribute : Attribute
{
    public int TimeoutMs { get; set; } = 30_000;
    public bool Cache { get; set; }
    public int CacheTtlSeconds { get; set; } = 300;
    public bool Metrics { get; set; } = true;
    public bool Tracing { get; set; } = true;
    public string? Description { get; set; }
}

/// <summary>Triggers cache invalidation when an event of the given type is observed.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class InvalidateCacheOnAttribute : Attribute
{
    public InvalidateCacheOnAttribute(Type eventType) => EventType = eventType;
    public Type EventType { get; }
    public string? Pattern { get; set; }
}

/// <summary>Annotates a command method to publish a domain event after success.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PublishDomainEventAttribute : Attribute
{
    public PublishDomainEventAttribute(string eventType) => EventType = eventType;
    public string EventType { get; }
}

namespace FireflyFramework.Eda.Annotations;

/// <summary>Marks a method whose result should be published as an event. Mirrors <c>@EventPublisher</c>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class EventPublisherAttribute : Attribute
{
    public string? Destination { get; set; }
    public string? EventType { get; set; }
    public string? Key { get; set; }
    public string? Condition { get; set; }
    public bool Async { get; set; }
    public int TimeoutMs { get; set; } = 5_000;
    public Events.PublisherType PublisherType { get; set; } = Events.PublisherType.Auto;
    public Events.SerializationFormat Serializer { get; set; } = Events.SerializationFormat.Json;
}

/// <summary>Marks a method as an event listener. Mirrors <c>@EventListener</c>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class EventListenerAttribute : Attribute
{
    public string[] Destinations { get; set; } = Array.Empty<string>();
    public string[] EventTypes { get; set; } = Array.Empty<string>();
    public Events.ConsumerType ConsumerType { get; set; } = Events.ConsumerType.Auto;
    public ErrorHandlingStrategy ErrorStrategy { get; set; } = ErrorHandlingStrategy.LogAndContinue;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1_000;
    public bool AutoAck { get; set; } = true;
    public string? GroupId { get; set; }
    public int Priority { get; set; }
}

public enum ErrorHandlingStrategy { LogAndContinue, Retry, DeadLetterQueue, Throw }

/// <summary>Publishes the method's return value after a successful invocation.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PublishResultAttribute : Attribute
{
    public string Destination { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public Events.PublisherType PublisherType { get; set; } = Events.PublisherType.Auto;
}

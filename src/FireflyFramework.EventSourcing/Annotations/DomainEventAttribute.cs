namespace FireflyFramework.EventSourcing.Annotations;

/// <summary>
/// Tags a class as a domain event. Equivalent to Java <c>@DomainEvent</c>: the
/// <see cref="EventType"/> is used as the persisted discriminator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DomainEventAttribute : Attribute
{
    public DomainEventAttribute(string eventType) => EventType = eventType;
    public string EventType { get; }
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public bool Publishable { get; set; } = true;
    public string[] Tags { get; set; } = Array.Empty<string>();
}

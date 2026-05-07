namespace FireflyFramework.Orchestration.Tcc;

[AttributeUsage(AttributeTargets.Class)]
public sealed class TccAttribute : Attribute
{
    public TccAttribute(string name) => Name = name;
    public string Name { get; }
    public int TimeoutMs { get; set; } = 60_000;
    public bool RetryEnabled { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int BackoffMs { get; set; } = 200;
    public string? TriggerEventType { get; set; }
}

[AttributeUsage(AttributeTargets.Class)] public sealed class TccParticipantAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)] public sealed class TryMethodAttribute : Attribute { public int TimeoutMs { get; set; } = 30_000; public int Retry { get; set; } public int BackoffMs { get; set; } = 100; }
[AttributeUsage(AttributeTargets.Method)] public sealed class ConfirmMethodAttribute : Attribute { public int TimeoutMs { get; set; } = 30_000; public int Retry { get; set; } public int BackoffMs { get; set; } = 100; }
[AttributeUsage(AttributeTargets.Method)] public sealed class CancelMethodAttribute : Attribute { public int TimeoutMs { get; set; } = 30_000; public int Retry { get; set; } public int BackoffMs { get; set; } = 100; }
[AttributeUsage(AttributeTargets.Parameter)] public sealed class FromTryAttribute : Attribute { public string Source { get; set; } = string.Empty; }

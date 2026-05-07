namespace FireflyFramework.Orchestration.Saga;

/// <summary>Marks a class as a saga. Mirrors <c>@Saga</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class SagaAttribute : Attribute
{
    public SagaAttribute(string name) => Name = name;
    public string Name { get; }
    public int LayerConcurrency { get; set; } = 1;
    public string? TriggerEventType { get; set; }
}

/// <summary>Marks a method as a saga step. Mirrors <c>@SagaStep</c>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SagaStepAttribute : Attribute
{
    public SagaStepAttribute(string id) => Id = id;
    public string Id { get; }
    public string? Compensate { get; set; }
    public string[] DependsOn { get; set; } = Array.Empty<string>();
    public int Retry { get; set; }
    public int BackoffMs { get; set; } = 100;
    public int TimeoutMs { get; set; } = 30_000;
    public bool Jitter { get; set; }
    public double JitterFactor { get; set; } = 0.1;
    public string? IdempotencyKey { get; set; }
    public bool CpuBound { get; set; }
    public int CompensationRetry { get; set; }
    public int CompensationTimeoutMs { get; set; } = 30_000;
    public int CompensationBackoffMs { get; set; } = 200;
    public bool CompensationCritical { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class CompensationSagaStepAttribute : Attribute { public string ForStepId { get; set; } = string.Empty; }

[AttributeUsage(AttributeTargets.Method)]
public sealed class OnSagaCompleteAttribute : Attribute { public bool Async { get; set; } public bool SuppressError { get; set; } }

[AttributeUsage(AttributeTargets.Method)]
public sealed class OnSagaErrorAttribute : Attribute { public Type[] ErrorTypes { get; set; } = Array.Empty<Type>(); public bool SuppressError { get; set; } public bool Async { get; set; } }

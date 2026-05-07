# FireflyFramework.Aop

Aspect-Oriented Programming module mirroring Spring AOP and pyfly `aop`.

## What it provides

| Concept | .NET form |
|---|---|
| `@Aspect` class | `[Aspect]` + `IFireflyAspect` marker |
| `@Before`, `@After`, `@Around`, `@AfterReturning`, `@AfterThrowing` | matching attributes |
| `JoinPoint` | reflective record of the call (target, method, args) |
| `ProceedingJoinPoint` | hands the around-advice a delegate to call the target |
| Pointcut DSL | `execution(...)`, `within(...)`, `@annotation(...)` |
| Aspect ordering | `[Aspect(Order=N)]` |

## Why a separate module?

The kernel/cqrs/orchestration modules each implement their own
cross-cutting concerns (validation, authorization, caching, tracing) the
same way Spring's templated aspects do — but they don't expose a
*generic* AOP facility for application code. This module fills the gap:
once you ship an aspect, any hand-rolled service can declare advice
without re-implementing weaving infrastructure.

## Quick start

```csharp
[Aspect(Order = 0)]
public sealed class TimingAspect : IFireflyAspect
{
    [Around("execution(* My.App.Services.*.* (..))")]
    public object? Time(ProceedingJoinPoint jp)
    {
        var sw = Stopwatch.StartNew();
        try { return jp.Proceed(); }
        finally { _logger.LogInformation("{Method} took {Ms}ms", jp.Method.Name, sw.ElapsedMilliseconds); }
    }
}

services.AddFireflyAop().AddAspect<TimingAspect>();
```

For simple call-site weaving inside a handler:

```csharp
var jp = new JoinPoint(this, GetType(), method, args, $"{GetType().Name}.{method.Name}");
return AdviceInvoker.Run(registry, jp, a => method.Invoke(this, a));
```

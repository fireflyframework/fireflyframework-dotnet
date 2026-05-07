# FireflyFramework.Kernel

Foundational layer of the Firefly Framework. Provides the unified exception hierarchy and shared abstractions that every other module depends on. Mirrors `org.fireflyframework:fireflyframework-kernel:26.04.01`.

## What's inside

| Type | Purpose |
|---|---|
| `FireflyException` | Root exception. Carries `ErrorCode` and an open `Context` dictionary so callers can attach diagnostic data. |
| `FireflyInfrastructureException` | Database / cache / messaging / networking failures. |
| `FireflySecurityException` | Authentication / authorization failures. |
| `FireflyVersion` | Static accessor for the calendar version (`26.04.01`). |

## Usage

```csharp
throw new FireflyException("Withdrawal exceeds limit", "WITHDRAWAL_LIMIT", new Dictionary<string, object?>
{
    ["accountId"] = accountId,
    ["requested"] = amount,
    ["limit"] = ceiling,
}, cause: null);
```

Because every business exception in the framework derives from `FireflyException`, downstream layers (`FireflyFramework.Web`'s `GlobalExceptionHandlerMiddleware`) can read `ErrorCode` and `Context` uniformly without reflecting on concrete types.

## Notes

The kernel has zero framework dependencies — only `Microsoft.Extensions.Logging.Abstractions`. Keep it that way.

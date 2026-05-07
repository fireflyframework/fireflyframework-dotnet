# FireflyFramework.Shell

Spring Shell port — interactive shell + `CommandLineRunner` /
`ApplicationRunner` for .NET hosts.

## What it provides

| Concept | .NET form |
|---|---|
| `@ShellComponent` | `[ShellComponent]` + `IFireflyShellComponent` marker |
| `@ShellMethod` | `[ShellMethod(Name = "...", Description = "...")]` |
| `@ShellOption` | `[ShellOption(Long = "...", Required = true)]` on a parameter |
| `@ShellArgument` | `[ShellArgument]` on a positional parameter |
| `CommandLineRunner` | `ICommandLineRunner` (raw argv) |
| `ApplicationRunner` | `IApplicationRunner` (parsed `IApplicationArguments`) |
| `ShellRunner` | `IShellRunner.RunOnceAsync(args)` / `RunInteractiveAsync()` |

## Quick start

```csharp
[ShellComponent]
public sealed class OrdersAdmin : IFireflyShellComponent
{
    [ShellMethod(Description = "Cancel an order")]
    public async Task Cancel(
        [ShellArgument(Name = "orderId")] Guid orderId,
        [ShellOption(Long = "reason", DefaultValue = "user-request")] string reason)
    {
        await _orders.CancelAsync(orderId, reason);
        Console.WriteLine($"cancelled {orderId} ({reason})");
    }
}

services.AddFireflyShell().AddShellComponent<OrdersAdmin>();
```

```bash
dotnet run -- cancel 0x12 --reason=fraud
```

For non-interactive `CommandLineRunner` style:

```csharp
public sealed class WarmCachesRunner : ICommandLineRunner
{
    public Task RunAsync(string[] args, CancellationToken ct) => _cache.WarmAsync(ct);
}

services.AddFireflyShell().AddCommandLineRunner<WarmCachesRunner>();
```

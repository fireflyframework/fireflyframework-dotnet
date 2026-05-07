# FireflyFramework.Testing

Test support library for Firefly modules — `FireflyTestBase`,
`FireflyTestClient`, `EventCapturePublisher`, slice attributes
(`[DataTest]`, `[ServiceTest]`, `[WebTest]`), and an `AssertEventPublished`
extension. Mirrors pyfly's `testing` module and Spring Boot Test.

## What it gives you

| Need | API |
|---|---|
| Build a host with framework wiring for a test | `class MyTest : FireflyTestBase` + override `ConfigureServices` |
| Replace a service with a mock | `services.ReplaceWithMock<IFoo, FakeFoo>(fake)` |
| Capture published events instead of dispatching them | `services.ReplaceWithCaptureEventPublisher()` |
| Assert an event was emitted | `publisher.AssertEventPublished<OrderCreated>()` |
| Talk to a controller without booting Kestrel | `new FireflyTestClient(testServer.CreateClient())` |
| Mark a test as a slice | `[DataTest]`, `[ServiceTest]`, `[WebTest]` |

## Quick example

```csharp
[ServiceTest]
public sealed class CreateOrderHandlerTests : FireflyTestBase
{
    private EventCapturePublisher _events = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddFireflyCqrs();
        _events = new EventCapturePublisher();
        services.ReplaceWithCaptureEventPublisher();
    }

    [Fact]
    public async Task Emits_OrderCreated()
    {
        await StartAsync();
        var bus = GetService<ICommandBus>();
        await bus.SendAsync(new CreateOrder(Guid.NewGuid(), 100));
        GetService<EventCapturePublisher>().AssertEventPublished<OrderCreated>();
    }
}
```

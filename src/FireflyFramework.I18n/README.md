# FireflyFramework.I18n

Spring i18n / `MessageSource` port. Loads JSON message bundles from
disk and resolves the request locale through a pluggable
`ILocaleResolver`.

## Bundle layout

```
i18n/
  messages.json           # invariant fallback
  messages_en.json
  messages_es.json
  messages_pt-BR.json
```

```json
{ "user.welcome": "Welcome {0}!", "order.notFound": "Order not found" }
```

## Quick start

```csharp
services.AddFireflyI18n(Configuration);

public sealed class WelcomeService
{
    public WelcomeService(IMessageSource messages, ILocaleResolver resolver) { ... }

    public string Welcome(string name, IDictionary<string, string?> hints)
    {
        var culture = _resolver.Resolve(hints);
        return _messages.GetMessage("user.welcome", culture, name);
    }
}
```

```yaml
Firefly:
  I18n:
    DefaultLocale: en
    ResourceDirectory: i18n
    BaseName: messages
    Resolver: AcceptHeader   # or Fixed / Cookie
```

# FireflyFramework.Notifications.Core

Service-layer abstractions on top of `FireflyFramework.Notifications`:
single-channel facades, a Scriban-backed template engine, a unified
dispatcher that respects user preferences, and a pluggable preference
store.

Mirrors the service classes in `org.fireflyframework:firefly-notifications`.

## Public surface

### Channel facades

```csharp
public sealed class EmailService
{
    public Task<EmailResponse> SendAsync(EmailRequest request, CancellationToken ct = default);
    public Task<EmailResponse> SendTemplateAsync(EmailTemplateRequest request, string from, string subject, CancellationToken ct = default);
}

public sealed class SmsService
{
    public Task<SmsResponse> SendAsync(SmsRequest request, CancellationToken ct = default);
}

public sealed class PushService
{
    public Task<PushNotificationResponse> SendAsync(PushNotificationRequest request, CancellationToken ct = default);
}
```

### Template engine

```csharp
public interface INotificationTemplateEngine
{
    Task<string> RenderAsync(string templateId, IDictionary<string, object?> variables, CancellationToken ct = default);
}

public sealed class ScribanTemplateEngine : INotificationTemplateEngine
{
    public ScribanTemplateEngine(Func<string, CancellationToken, Task<string>> loader);
}
```

The loader closure receives the template id and returns the raw
template body — load from disk, S3, or your CMS as needed.

### Dispatcher

`NotificationDispatcher` is the recommended entry point. It routes a
request to the right channel and skips delivery when a user has
disabled the channel.

```csharp
using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Core;

var dispatcher = new NotificationDispatcher(
    log:         logger,
    email:       new EmailService(emailProvider),
    sms:         new SmsService(smsProvider),
    push:        new PushService(pushProvider),
    preferences: preferenceService);

await dispatcher.SendEmailAsync(
    userId:  "alice",
    request: new EmailRequest(
        From: "no-reply@example.com",
        To:   new[] { "alice@example.com" },
        Cc:   null, Bcc: null,
        Subject: "Welcome",
        Text:    null,
        Html:    "<p>Welcome, Alice.</p>"));
```

If the user has `EmailEnabled = false` the dispatcher returns an
`EmailResponse` with `Success = false` and the message
`"email channel disabled by user preferences"` without calling the
provider.

### Preference store

```csharp
public interface INotificationPreferenceService
{
    Task<NotificationPreferenceDto?> GetAsync(string userId, CancellationToken ct = default);
    Task                             UpdateAsync(NotificationPreferenceDto preferences, CancellationToken ct = default);
    Task<bool>                       IsChannelEnabledAsync(string userId, string channel, CancellationToken ct = default);
}

public sealed class InMemoryNotificationPreferenceService : INotificationPreferenceService { ... }
```

Replace the in-memory default with an EF Core implementation in
production.

## Dependencies

| Reference                            | Used for                       |
|--------------------------------------|--------------------------------|
| `FireflyFramework.Notifications`     | Provider contracts and DTOs    |
| `Scriban`                            | Template rendering             |

## Java mapping

| .NET                                       | Java                                     |
|--------------------------------------------|------------------------------------------|
| `EmailService` / `SmsService` / `PushService` | `EmailService` / `SmsService` / `PushService` |
| `ScribanTemplateEngine`                    | `FreeMarkerTemplateEngine`               |
| `NotificationDispatcher`                   | `NotificationDispatcher`                 |
| `INotificationPreferenceService`           | `NotificationPreferenceService`          |

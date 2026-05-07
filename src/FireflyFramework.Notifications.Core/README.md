# FireflyFramework.Notifications.Core

## Overview

`FireflyFramework.Notifications.Core` is the **service layer** for the
notification subsystem. It sits one level above the port project
(`FireflyFramework.Notifications`) and orchestrates everything an
application typically wants to do beyond raw provider calls:

- **Channel facades** — `EmailService`, `SmsService`, and `PushService`
  are thin wrappers around `IEmailProvider`, `ISmsProvider`, and
  `IPushProvider`, plus an opinion: `EmailService` accepts an optional
  template engine so callers can send a templated message in one call.
- **Template engine** — `INotificationTemplateEngine` with a Scriban-
  backed default. The engine resolves a template by id (via a caller-
  supplied loader closure), renders it with a variables dictionary,
  and returns the rendered string. Scriban is the .NET analogue of
  the Java module's FreeMarker engine.
- **Dispatcher** — `NotificationDispatcher` is the recommended entry
  point for application code. It routes a request to the right channel
  service and consults a pluggable preference store before dispatching,
  so a user who has opted out of email never has an email sent on
  their behalf.
- **Preference store** — `INotificationPreferenceService` plus a default
  in-memory implementation suitable for tests and stand-alone hosts;
  production systems plug in an EF Core or Redis adapter.

This module mirrors the service classes in
`org.fireflyframework:firefly-notifications-core`. The service shapes
match one-for-one (`EmailService` ↔ `EmailService`,
`NotificationDispatcher` ↔ `NotificationDispatcher`,
`InMemoryNotificationPreferenceService` ↔
`InMemoryNotificationPreferenceService`), with the only deliberate
deviation being the template engine: Scriban replaces FreeMarker as
the idiomatic .NET choice, but the surface (`RenderAsync(templateId,
variables, ct)`) is identical.

## When to use this module

Reference `Notifications.Core` when:

- You want a single dispatch entry point that respects user
  preferences. Most applications should depend on
  `NotificationDispatcher`, never on the providers directly.
- You want to send templated emails (`Welcome`, `PasswordReset`,
  `OrderConfirmation`) without templating logic in your domain code.
- You want unit tests against the dispatcher contract that do not
  require a real preference store.

Stay in `FireflyFramework.Notifications` (the port project) when:

- You're writing a plugin that ships a new adapter — the port is the
  whole contract you need to satisfy.
- You explicitly want to bypass preference checks (e.g. a transactional
  receipt that must always send regardless of marketing prefs). Call
  the provider directly in that case, and document the bypass.

## Mental model

```
                            application code
                                   │
                                   │ uses
                                   ▼
                       ┌─────────────────────────┐
                       │ NotificationDispatcher  │
                       │  (channel routing +     │
                       │   preference gating)    │
                       └────────────┬────────────┘
                                    │ delegates
              ┌─────────────────────┼──────────────────────┐
              ▼                     ▼                      ▼
       ┌─────────────┐       ┌────────────┐         ┌─────────────┐
       │EmailService │       │ SmsService │         │ PushService │
       │(+template   │       │            │         │             │
       │  engine)    │       │            │         │             │
       └──────┬──────┘       └─────┬──────┘         └──────┬──────┘
              │                    │                       │
              ▼                    ▼                       ▼
        IEmailProvider       ISmsProvider             IPushProvider
        (SendGrid /          (Twilio)                 (Firebase)
         Resend)
```

`EmailService.SendTemplateAsync` is the only call site where the
template engine participates: it resolves the template body via the
loader closure, renders it as HTML, and forwards a populated
`EmailRequest` to the provider.

## Quick start

```csharp
using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Core;
using FireflyFramework.Notifications.SendGrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// 1. Configure providers (per-channel — one or more).
services.Configure<SendGridOptions>(config.GetSection(SendGridOptions.SectionName));
services.AddSingleton<IEmailProvider, SendGridEmailProvider>();

// 2. Wrap in a channel facade.
services.AddSingleton(sp => new EmailService(
    sp.GetRequiredService<IEmailProvider>(),
    new ScribanTemplateEngine(LoadTemplate)));

// 3. Add a preference store (in-memory for dev; replace in prod).
services.AddSingleton<INotificationPreferenceService, InMemoryNotificationPreferenceService>();

// 4. Compose the dispatcher.
services.AddSingleton(sp => new NotificationDispatcher(
    sp.GetRequiredService<ILogger<NotificationDispatcher>>(),
    email:       sp.GetRequiredService<EmailService>(),
    preferences: sp.GetRequiredService<INotificationPreferenceService>()));

// Application code:
public sealed class WelcomeFlow
{
    private readonly NotificationDispatcher _dispatcher;
    public WelcomeFlow(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task SendWelcomeAsync(string userId, string email, CancellationToken ct) =>
        _dispatcher.SendEmailAsync(userId,
            new EmailRequest(
                From:    "no-reply@example.com",
                To:      new[] { email },
                Cc:      null, Bcc: null,
                Subject: "Welcome",
                Text:    null,
                Html:    "<p>Welcome aboard.</p>"),
            ct);
}

static Task<string> LoadTemplate(string id, CancellationToken ct) =>
    File.ReadAllTextAsync($"templates/{id}.scriban", ct);
```

## Public surface

### Channel facades

| Type           | Methods                                                                             |
|----------------|-------------------------------------------------------------------------------------|
| `EmailService` | `SendAsync(EmailRequest, ct)` and `SendTemplateAsync(EmailTemplateRequest, from, subject, ct)` |
| `SmsService`   | `SendAsync(SmsRequest, ct)`                                                         |
| `PushService`  | `SendAsync(PushNotificationRequest, ct)`                                            |

`EmailService` accepts an optional `INotificationTemplateEngine`. When
absent, `SendTemplateAsync` returns
`new EmailResponse(null, false, "No template engine configured")` —
matching the Java module's behaviour.

### Template engine

```csharp
public interface INotificationTemplateEngine
{
    Task<string> RenderAsync(string templateId,
                             IDictionary<string, object?> variables,
                             CancellationToken ct = default);
}

public sealed class ScribanTemplateEngine : INotificationTemplateEngine
{
    public ScribanTemplateEngine(Func<string, CancellationToken, Task<string>> loader);
}
```

The loader closure is the abstraction over storage — disk, S3,
Postgres `bytea`, your CMS, your config server. The engine only
cares that it gets the raw template body for the requested id.

`ScribanTemplateEngine.RenderAsync` parses the loaded source via
`Scriban.Template.Parse(source, templateId)` (the second argument is
used as the source filename in error messages). The variables
dictionary is rendered with Scriban's standard syntax (`{{ name }}`,
`{{ for item in items }}…`).

### Dispatcher

```csharp
public sealed class NotificationDispatcher
{
    public NotificationDispatcher(
        ILogger<NotificationDispatcher> log,
        EmailService? email = null,
        SmsService?   sms   = null,
        PushService?  push  = null,
        INotificationPreferenceService? preferences = null);

    public Task<EmailResponse>            SendEmailAsync(string userId, EmailRequest request, CancellationToken ct = default);
    public Task<SmsResponse>              SendSmsAsync  (string userId, SmsRequest   request, CancellationToken ct = default);
    public Task<PushNotificationResponse> SendPushAsync (string userId, PushNotificationRequest request, CancellationToken ct = default);
}
```

Behaviour, pinned by `NotificationDispatcherTests`:

- If the matching channel service is `null`, the dispatcher returns a
  failure response with the message `"no <channel> provider registered"`
  rather than throwing. This lets you wire only the channels you need
  and still receive a uniform response.
- If a `INotificationPreferenceService` is configured and reports
  the channel as disabled for `userId`, the dispatcher returns a
  failure response with `"<channel> channel disabled by user
  preferences"` and never invokes the provider.
- When no preference store is configured, every send proceeds.

### Preference store

```csharp
public interface INotificationPreferenceService
{
    Task<NotificationPreferenceDto?> GetAsync(string userId, CancellationToken ct = default);
    Task                              UpdateAsync(NotificationPreferenceDto preferences, CancellationToken ct = default);
    Task<bool>                        IsChannelEnabledAsync(string userId, string channel, CancellationToken ct = default);
}

public sealed class InMemoryNotificationPreferenceService : INotificationPreferenceService { ... }
```

The default in-memory implementation:

- Stores preferences in a `ConcurrentDictionary<string, NotificationPreferenceDto>`.
- `IsChannelEnabledAsync` returns `true` (default-allow) for any user
  who has not been registered.
- The `channel` argument is matched case-insensitively against
  `"email"`, `"sms"`, `"push"`. Any other value defaults to `true`.

## Configuration

This module exposes no `*Options` of its own. Every knob is supplied
either through DI registration (which channels exist, which provider
is wired, which preference store) or through the underlying provider
adapters. A typical configuration:

```json
{
  "Firefly": {
    "Notifications": {
      "SendGrid":  { "ApiKey": "<sendgrid-api-key>" },
      "Twilio":    { "AccountSid": "...", "AuthToken": "...", "DefaultFromNumber": "+34000000000" },
      "Firebase":  { "CredentialsPath": "/secrets/firebase.json", "ProjectId": "firefly-prod" }
    }
  }
}
```

The dispatcher itself only has constructor parameters; pick the
combination of channels and preference store that fits your
application.

## Common patterns

### 1. Preference-aware send

```csharp
var resp = await dispatcher.SendEmailAsync("alice", new EmailRequest(
    From:    "no-reply@example.com",
    To:      new[] { "alice@example.com" },
    Cc:      null, Bcc: null,
    Subject: "Welcome",
    Text:    null,
    Html:    "<p>Welcome, Alice.</p>"));

if (!resp.Success && resp.ErrorMessage?.Contains("disabled by user preferences") == true)
{
    // User has opted out — record the suppression and move on.
}
```

### 2. Templated send

```csharp
var engine = new ScribanTemplateEngine(
    (id, ct) => File.ReadAllTextAsync($"templates/{id}.scriban", ct));
var emails = new EmailService(sendGridProvider, engine);

var resp = await emails.SendTemplateAsync(
    request: new EmailTemplateRequest(
        TemplateId: "welcome",
        Variables:  new Dictionary<string, object?> { ["name"] = "Alice" },
        Recipients: new[] { "alice@example.com" }),
    from:    "no-reply@example.com",
    subject: "Welcome");
```

`templates/welcome.scriban`:

```
<p>Hello {{ name }}!</p>
<p>We're glad to have you.</p>
```

This is exactly the shape the test suite asserts in
`NotificationsTests.EmailService_renders_template_via_template_engine`.

### 3. Multi-channel fan-out

```csharp
public sealed class CriticalAlertService
{
    private readonly NotificationDispatcher _dispatcher;
    public CriticalAlertService(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task NotifyAsync(string userId, string title, string body, CancellationToken ct)
    {
        await Task.WhenAll(
            _dispatcher.SendEmailAsync(userId, new EmailRequest(
                From:    "alerts@example.com",
                To:      new[] { LookupEmail(userId) },
                Cc:      null, Bcc: null,
                Subject: title,
                Text:    body,
                Html:    null), ct),
            _dispatcher.SendSmsAsync(userId, new SmsRequest(
                PhoneNumber: LookupPhone(userId),
                Message:     $"{title}: {body}",
                FromNumber:  null), ct),
            _dispatcher.SendPushAsync(userId, new PushNotificationRequest(
                Token: LookupDeviceToken(userId),
                Title: title,
                Body:  body)));
    }
}
```

Each channel is gated independently — a user who has only enabled
push gets one delivery, not three failures.

### 4. EF Core preference store

```csharp
public sealed class EfPreferenceService : INotificationPreferenceService
{
    private readonly AppDb _db;
    public EfPreferenceService(AppDb db) => _db = db;

    public async Task<NotificationPreferenceDto?> GetAsync(string userId, CancellationToken ct = default)
    {
        var row = await _db.NotificationPreferences.FindAsync(new object?[] { userId }, ct);
        return row is null ? null : new NotificationPreferenceDto(
            row.UserId, row.EmailEnabled, row.SmsEnabled, row.PushEnabled);
    }

    public async Task UpdateAsync(NotificationPreferenceDto p, CancellationToken ct = default)
    {
        var existing = await _db.NotificationPreferences.FindAsync(new object?[] { p.UserId }, ct);
        if (existing is null)
        {
            _db.NotificationPreferences.Add(new(p.UserId, p.EmailEnabled, p.SmsEnabled, p.PushEnabled));
        }
        else
        {
            existing.EmailEnabled = p.EmailEnabled;
            existing.SmsEnabled   = p.SmsEnabled;
            existing.PushEnabled  = p.PushEnabled;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> IsChannelEnabledAsync(string userId, string channel, CancellationToken ct = default)
    {
        var p = await GetAsync(userId, ct);
        if (p is null) return true;
        return channel.ToLowerInvariant() switch
        {
            "email" => p.EmailEnabled,
            "sms"   => p.SmsEnabled,
            "push"  => p.PushEnabled,
            _       => true,
        };
    }
}
```

## Pitfalls and gotchas

- **Default-allow.** `IsChannelEnabledAsync` returns `true` for unknown
  users. This is intentional — it keeps onboarding flows working
  before the user has had a chance to set preferences — but it does
  mean you must explicitly call `UpdateAsync` after a user opts out.

- **The dispatcher returns failures, not exceptions, for missing
  providers.** A common surprise: a service registered for email-only
  receives an SMS request and gets back `Success = false`,
  `ErrorMessage = "no sms provider registered"`. Inspect the response,
  do not assume success.

- **Scriban is not Liquid.** The Java module uses FreeMarker; .NET uses
  Scriban. Both render `{{ name }}` the same way, but advanced
  expressions differ. When porting templates, consult the
  [Scriban language reference](https://github.com/scriban/scriban/blob/master/doc/language.md).

- **The template loader closure is invoked on every send.** If you load
  from disk on a hot path, cache the parsed template at the loader
  level — the engine itself does not cache.

  ```csharp
  // Bad — re-reads the file on every send.
  new ScribanTemplateEngine((id, ct) => File.ReadAllTextAsync(Path(id), ct))

  // Good — cached fetch.
  var cache = new ConcurrentDictionary<string, string>();
  new ScribanTemplateEngine((id, _) =>
      Task.FromResult(cache.GetOrAdd(id, k => File.ReadAllText(Path(k)))))
  ```

- **`SendTemplateAsync` only sets the HTML body.** It populates
  `EmailRequest.Html` with the rendered output and leaves `Text = null`.
  Provide a separate plain-text template through your own helper if
  you need multipart/alternative messages.

## Internals (for the curious)

The dispatcher's preference check is one short helper:

```csharp
private async Task<bool> BlockedByPreferences(string userId, string channel, CancellationToken ct)
{
    if (_preferences is null) return false;
    return !await _preferences.IsChannelEnabledAsync(userId, channel, ct).ConfigureAwait(false);
}
```

This deliberately returns `false` (not blocked) when no preference
service is wired — the absence of configuration must not prevent
delivery. The early-return pattern keeps the dispatcher small enough
to read top-to-bottom and assert against in unit tests.

The channel facades (`EmailService`, `SmsService`, `PushService`) are
not abstract — there is no `INotificationService` interface — because
each channel has different DTOs and the dispatcher composes them
explicitly. Adding a new channel means adding a new facade and a new
dispatcher overload, by design.

`InMemoryNotificationPreferenceService` uses `ConcurrentDictionary`
for thread safety. There are no eviction or quota policies — for any
non-trivial deployment, swap in a persistent implementation.

## Dependencies

| Reference                                       | Purpose                                |
|-------------------------------------------------|----------------------------------------|
| `FireflyFramework.Notifications` (project)      | Provider ports and DTOs                |
| `FireflyFramework.Utils` (project)              | Shared utility helpers                 |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | DI registration helpers        |
| `Microsoft.Extensions.Logging.Abstractions`     | Dispatcher logging                     |
| `Scriban`                                       | Template engine (FreeMarker analogue)  |

## Java mapping

| .NET                                        | Java                                       |
|---------------------------------------------|--------------------------------------------|
| `EmailService` / `SmsService` / `PushService` | `EmailService` / `SmsService` / `PushService` |
| `INotificationTemplateEngine`               | `NotificationTemplateEngine`               |
| `ScribanTemplateEngine`                     | `FreeMarkerTemplateEngine`                 |
| `NotificationDispatcher`                    | `NotificationDispatcher`                   |
| `INotificationPreferenceService`            | `NotificationPreferenceService`            |
| `InMemoryNotificationPreferenceService`     | `InMemoryNotificationPreferenceService`    |

# FireflyFramework.Notifications

## Overview

`FireflyFramework.Notifications` is the **port** project for the
notifications subsystem. It defines the channel-agnostic contracts that
the rest of the framework depends on — three provider interfaces
(`IEmailProvider`, `ISmsProvider`, `IPushProvider`), the request /
response DTOs they exchange, and the supporting records for templates,
attachments, statuses, and user preferences. There is no runtime
behaviour in this assembly: it is pure shape, pure contract.

Why is this a separate project? In a hexagonal architecture the
domain code should depend on a stable, narrow interface — never on a
specific vendor SDK. By placing the ports here, downstream code
references a single 1 KB DTO assembly instead of pulling in SendGrid,
Twilio, FirebaseAdmin, and Resend transitively. Adapters
(`FireflyFramework.Notifications.SendGrid`,
`...Twilio`, `...Resend`, `...Firebase`) implement the ports and live
in their own packages so each vendor's transitive dependency tree is
opt-in.

This module mirrors the Java
`org.fireflyframework:firefly-notifications` artifact — the contract
project of the upstream notifications stack. The DTO field names, the
provider method signatures, and the lifecycle states are deliberately
kept aligned so cross-stack code review and SDK generation against
both ports yield identical wire shapes.

The high-level service surface (templates, dispatcher, preference
fan-out) lives one layer up in
`FireflyFramework.Notifications.Core`. Most application code consumes
that — this assembly is the building block they share.

## When to use this module

Reference `FireflyFramework.Notifications` directly from:

- **Plugin assemblies** that ship a brand-new email / SMS / push
  adapter. Implementing `IEmailProvider` is the entire contract; you
  do not need to depend on the dispatcher or template engine.
- **Domain modules** that want to fire a notification but stay
  vendor-agnostic. They take an `IEmailProvider` (or `IPushProvider`,
  `ISmsProvider`) by constructor injection and never see SendGrid /
  Twilio types.
- **Test fakes** that record sent messages for assertion. Implementing
  the interface against a `List<EmailRequest>` is a one-liner.

Do **not** use this module when you want preference checks, template
rendering, or routing — that belongs to
`FireflyFramework.Notifications.Core`. Likewise, do not implement the
ports inline in application code; package adapter classes in their own
project so the vendor SDK stays out of the application's dependency
graph.

## Mental model

```
                   ┌──────────────────────────────────────┐
                   │  FireflyFramework.Notifications      │   <-- this project (ports + DTOs)
                   │  IEmailProvider / ISmsProvider /     │
                   │  IPushProvider + records             │
                   └──────────────────────────────────────┘
                          ▲                           ▲
                          │                           │
                  implements                       depends on
                          │                           │
   ┌──────────────────────┴────────────┐   ┌──────────┴───────────────┐
   │ adapters                          │   │ services                 │
   │  Notifications.SendGrid           │   │ Notifications.Core:      │
   │  Notifications.Resend             │   │  EmailService            │
   │  Notifications.Twilio             │   │  SmsService              │
   │  Notifications.Firebase           │   │  PushService             │
   └───────────────────────────────────┘   │  NotificationDispatcher  │
                                           └──────────────────────────┘
```

A **port** is one of `IEmailProvider`, `ISmsProvider`, or
`IPushProvider`. An **adapter** is a class that implements one of those
ports against a vendor SDK or REST API. Every adapter receives a
strongly-typed `*Request` record and returns a strongly-typed
`*Response`. The port contract guarantees:

- Calls are asynchronous and cancellable.
- Failures are reported in the response (`Success = false`,
  `ErrorMessage` populated) — they do not surface as exceptions for
  ordinary delivery problems. Adapters may still throw on
  programming errors (null arguments, misconfiguration).
- Successful sends populate `MessageId` with whatever the provider
  returns (SendGrid's `X-Message-Id`, Twilio's `Sid`, Resend's `id`,
  FCM's message-name path).

## Quick start

A custom in-memory adapter that captures sent emails:

```csharp
using FireflyFramework.Notifications;

public sealed class CapturingEmailProvider : IEmailProvider
{
    public List<EmailRequest> Sent { get; } = new();

    public Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
    {
        Sent.Add(request);
        return Task.FromResult(new EmailResponse(
            MessageId: Guid.NewGuid().ToString("N"),
            Success: true,
            ErrorMessage: null));
    }
}
```

A consumer that takes the port by constructor injection:

```csharp
using FireflyFramework.Notifications;

public sealed class WelcomeEmailService
{
    private readonly IEmailProvider _emails;

    public WelcomeEmailService(IEmailProvider emails) => _emails = emails;

    public Task<EmailResponse> WelcomeAsync(string toAddress, CancellationToken ct = default) =>
        _emails.SendEmailAsync(
            new EmailRequest(
                From:    "no-reply@example.com",
                To:      new[] { toAddress },
                Cc:      null,
                Bcc:     null,
                Subject: "Welcome",
                Text:    "Welcome aboard.",
                Html:    "<p>Welcome aboard.</p>"),
            ct);
}
```

Wire the chosen adapter at the composition root:

```csharp
builder.Services.Configure<SendGridOptions>(
    builder.Configuration.GetSection(SendGridOptions.SectionName));
builder.Services.AddSingleton<IEmailProvider, SendGridEmailProvider>();
builder.Services.AddScoped<WelcomeEmailService>();
```

## Public surface

### Provider ports

| Type             | Description                                                         |
|------------------|---------------------------------------------------------------------|
| `IEmailProvider` | Sends an `EmailRequest`, returns `EmailResponse`. Implemented by SendGrid + Resend adapters. |
| `ISmsProvider`   | Sends an `SmsRequest`, returns `SmsResponse`. Implemented by the Twilio adapter. |
| `IPushProvider`  | Sends a `PushNotificationRequest`, returns `PushNotificationResponse`. Implemented by the Firebase adapter. |

### Email DTOs

| Type                   | Members                                                                                |
|------------------------|----------------------------------------------------------------------------------------|
| `EmailRequest`         | `From`, `To`, `Cc?`, `Bcc?`, `Subject`, `Text?`, `Html?`, `Attachments?`               |
| `EmailResponse`        | `MessageId?`, `Success`, `ErrorMessage?`                                              |
| `EmailAttachment`      | `FileName`, `ContentType`, `Data` (raw `byte[]`, base-64-encoded by adapters at the boundary) |
| `EmailTemplateRequest` | `TemplateId`, `Variables` (`IDictionary<string, object?>`), `Recipients`              |
| `EmailStatus` (enum)   | `Pending`, `Sent`, `Failed`, `Bounced`                                                |

`EmailRequest` is a `sealed record` — every field is positional and
immutable. `To` is required; `Cc` and `Bcc` are optional (nullable).
Either `Text` or `Html` (or both) should be set; an adapter that
receives both will deliver a multipart/alternative message.

### SMS DTOs

| Type          | Members                                       |
|---------------|-----------------------------------------------|
| `SmsRequest`  | `PhoneNumber`, `Message`, `FromNumber?`       |
| `SmsResponse` | `MessageId?`, `Success`, `ErrorMessage?`      |

If `FromNumber` is `null` the adapter falls back to its configured
default (e.g. `TwilioOptions.DefaultFromNumber`).

### Push DTOs

| Type                        | Members                                          |
|-----------------------------|--------------------------------------------------|
| `PushNotificationRequest`   | `Token`, `Title`, `Body`, `Data?`                |
| `PushNotificationResponse`  | `MessageId?`, `Success`, `ErrorMessage?`         |

`Token` is a single device token — fan-out across multiple devices is
the caller's responsibility.

### Preferences

| Type                          | Members                                                              |
|-------------------------------|----------------------------------------------------------------------|
| `NotificationPreferenceDto`   | `UserId`, `EmailEnabled`, `SmsEnabled`, `PushEnabled` (all `bool`)   |

This DTO travels between the dispatcher (in `Core`) and the
preference store. The port project defines the shape so adapters that
report user-driven opt-in / opt-out can speak the same vocabulary.

## Configuration

This project exposes no configuration of its own. Adapter projects
each declare their own `*Options` class bound to a configuration
section under `Firefly:Notifications:<Provider>`:

| Adapter   | Options class       | Section                          |
|-----------|---------------------|----------------------------------|
| SendGrid  | `SendGridOptions`   | `Firefly:Notifications:SendGrid` |
| Twilio    | `TwilioOptions`     | `Firefly:Notifications:Twilio`   |
| Resend    | `ResendOptions`     | `Firefly:Notifications:Resend`   |
| Firebase  | `FirebaseOptions`   | `Firefly:Notifications:Firebase` |

See each adapter's README for the full schema.

## Common patterns

### 1. Define a new adapter

Implementing a port is the entire contract. Map the DTO into the
vendor's API, then translate the response back:

```csharp
public sealed class PostmarkEmailProvider : IEmailProvider
{
    private readonly HttpClient _http;

    public PostmarkEmailProvider(HttpClient http) => _http = http;

    public async Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/email", new
        {
            From      = request.From,
            To        = string.Join(',', request.To),
            Subject   = request.Subject,
            TextBody  = request.Text,
            HtmlBody  = request.Html,
        }, ct);

        if (!resp.IsSuccessStatusCode)
        {
            return new EmailResponse(null, false, await resp.Content.ReadAsStringAsync(ct));
        }

        var doc = await resp.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct);
        return new EmailResponse(doc?["MessageID"]?.ToString(), true, null);
    }
}
```

### 2. Build a fake for tests

```csharp
internal sealed class FakeEmailProvider : IEmailProvider
{
    public List<EmailRequest> Sent { get; } = new();
    public Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
    {
        Sent.Add(request);
        return Task.FromResult(new EmailResponse(Guid.NewGuid().ToString("N"), true, null));
    }
}
```

This mirrors `NotificationsTests.FakeEmailProvider` and is the pattern
the framework's own test suite uses.

### 3. Carry attachments

Attachments are always raw bytes — adapters base-64-encode at the
boundary if their wire protocol requires it (e.g. SendGrid):

```csharp
var data = await File.ReadAllBytesAsync("statement.pdf", ct);
var request = new EmailRequest(
    From:    "billing@example.com",
    To:      new[] { customer.Email },
    Cc:      null, Bcc: null,
    Subject: "Your statement",
    Text:    "Statement attached.",
    Html:    null,
    Attachments: new[]
    {
        new EmailAttachment("statement.pdf", "application/pdf", data),
    });
```

### 4. Capture the message id for audit

Every adapter returns the provider's native id in
`EmailResponse.MessageId` — `X-Message-Id` for SendGrid, `id` for
Resend, `Sid` for Twilio. Persist it in the audit log so future
support requests can be traced back to the upstream provider:

```csharp
var resp = await _emails.SendEmailAsync(request, ct);
if (resp.Success)
{
    await _audit.LogEmailSentAsync(userId, request.Subject, resp.MessageId, ct);
}
else
{
    _logger.LogWarning("email send failed: {Error}", resp.ErrorMessage);
}
```

## Pitfalls and gotchas

- **Don't throw for delivery failures.** The contract is to return
  `Success = false`. Throwing forces every caller into a try / catch
  that they otherwise would not need.

  ```csharp
  // Bad — caller now has to handle vendor-specific exceptions.
  if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(...);

  // Good — uniform shape for every adapter.
  if (!resp.IsSuccessStatusCode) return new EmailResponse(null, false, body);
  ```

- **`To` is required, but the records do not enforce non-empty.** An
  empty list is a programming error; adapters typically delegate
  validation to the vendor SDK, which returns a 4xx that surfaces
  through `ErrorMessage`.

- **`EmailAttachment.Data` is raw bytes, never base-64.** Pre-encoding
  doubles the payload size and breaks SendGrid's attachment handling.

- **`PushNotificationRequest.Token` is a device token, not a topic.**
  For broadcast / topic delivery use `FirebaseAdmin` directly — the
  adapter is intentionally device-targeted.

- **`EmailStatus` is informational.** Adapters do not consume it;
  application code uses it to project the lifecycle of a queued email
  through delivery / bounce.

## Internals (for the curious)

The DTOs are all `sealed record` types: positional members, value
equality, `with`-expressions for non-destructive updates. This gives
free deep equality in tests (`request1.Should().BeEquivalentTo(request2)`)
and immutable value semantics so DTOs are safe to share across
threads.

There are no setters, no factory methods, no validation logic. The
intent is for this assembly to be **trivially serialisable** —
System.Text.Json handles every record without configuration, and the
contracts are stable enough to keep wire compatibility across major
versions.

The lack of a base interface (e.g. `INotificationProvider`) is
deliberate: each channel has different DTOs and there is no useful
generalisation that would not erase types. The dispatcher in
`Core` composes the three channel services explicitly.

## Dependencies

| Reference                                  | Purpose                            |
|--------------------------------------------|------------------------------------|
| `FireflyFramework.Kernel` (project)        | Shared calendar version + base error types via transitive use |
| `Microsoft.Extensions.Options`             | `*Options` patterns used by adapters that consume this assembly |
| `Microsoft.Extensions.Logging.Abstractions`| Logger types referenced from adapter code that depends on this port |

## Java mapping

| .NET                          | Java equivalent                       |
|-------------------------------|---------------------------------------|
| `IEmailProvider`              | `EmailProvider`                       |
| `ISmsProvider`                | `SmsProvider`                         |
| `IPushProvider`               | `PushProvider`                        |
| `EmailRequest`                | `EmailRequest`                        |
| `EmailResponse`               | `EmailResponse`                       |
| `EmailAttachment`             | `EmailAttachment`                     |
| `EmailTemplateRequest`        | `EmailTemplateRequest`                |
| `EmailStatus`                 | `EmailStatus`                         |
| `SmsRequest` / `SmsResponse`  | `SmsRequest` / `SmsResponse`          |
| `PushNotificationRequest`     | `PushNotificationRequest`             |
| `PushNotificationResponse`    | `PushNotificationResponse`            |
| `NotificationPreferenceDto`   | `NotificationPreferenceDto`           |

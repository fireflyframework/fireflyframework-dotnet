# FireflyFramework.Notifications.Resend

## Overview

`FireflyFramework.Notifications.Resend` is the **Resend
implementation of `IEmailProvider`**. Resend (`https://resend.com`) is
a developer-first transactional email service with a clean REST API
and bearer-token auth — this adapter calls it directly without an
intermediate SDK package.

Mirrors `org.fireflyframework:firefly-notifications-resend`. The
endpoint, payload shape, and response contract are identical to the
Java line.

## Why a separate module?

Resend is one of several email providers Firefly supports
(`SendGrid`, `Twilio` for SMS, `Firebase` for push). Each lives in its
own assembly so a service that uses Resend doesn't have to take
SendGrid's SDK, Twilio's SDK, etc. The bundled
`FireflyFramework.Notifications` module defines the port
(`IEmailProvider`); each provider assembly implements it.

## Mental model

```
   application code
        │
        │  IEmailProvider.SendEmailAsync(EmailRequest)
        ▼
   ┌──────────────────────────┐
   │ ResendEmailProvider      │
   └──────────┬───────────────┘
              │
              ▼
   ┌──────────────────────────┐
   │ POST {BaseUrl}/emails    │
   │ Authorization: Bearer …  │  ← API key in header
   └──────────────────────────┘
```

The adapter is intentionally thin — it doesn't queue, retry, or
fan-out. Compose those concerns at a higher layer using the
`FireflyFramework.Notifications.Core` dispatcher.

## Configuration

```json
{
  "Firefly": {
    "Notifications": {
      "Resend": {
        "ApiKey":  "<resend api key>",
        "BaseUrl": "https://api.resend.com"
      }
    }
  }
}
```

| Property  | Default                  | Notes                                    |
|-----------|--------------------------|------------------------------------------|
| `ApiKey`  | (required)               | Bearer token from the Resend dashboard   |
| `BaseUrl` | `https://api.resend.com` | Override only for testing or self-hosted |

## Wiring

```csharp
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.AddHttpClient<IEmailProvider, ResendEmailProvider>();
```

The typed `HttpClient` registration brings in the standard pipeline,
so you can layer resilience handlers on top:

```csharp
builder.Services.AddHttpClient<IEmailProvider, ResendEmailProvider>()
    .AddStandardResilienceHandler();
```

## Behaviour

- Posts the request to `POST {BaseUrl}/emails` with the API key as a
  `Bearer` token.
- Returns `EmailResponse(MessageId: response.id, Success: true)` on
  2xx; otherwise `EmailResponse(null, false, responseBody)`.
- The adapter does **not** throw on 4xx/5xx — it returns
  `Success = false` with the response body. This lets the dispatcher
  decide whether to retry or escalate.

## Common patterns

### Sending a transactional email

```csharp
var result = await email.SendEmailAsync(new EmailRequest
{
    From    = "no-reply@firefly.app",
    To      = new[] { "ada@example.com" },
    Subject = "Welcome to Firefly",
    Html    = "<p>Hi Ada, please verify your email.</p>",
    Text    = "Hi Ada, please verify your email.",
}, ct);

if (!result.Success)
{
    log.LogWarning("Resend rejected the email: {Body}", result.ErrorMessage);
}
```

### Selecting Resend for marketing, SendGrid for transactional

```csharp
public sealed class SmartEmailDispatcher(
    [FromKeyedServices("resend")]   IEmailProvider resend,
    [FromKeyedServices("sendgrid")] IEmailProvider sendgrid)
{
    public Task<EmailResponse> SendAsync(EmailRequest req, EmailKind kind, CancellationToken ct) =>
        kind switch
        {
            EmailKind.Marketing     => resend.SendEmailAsync(req, ct),
            EmailKind.Transactional => sendgrid.SendEmailAsync(req, ct),
            _                       => sendgrid.SendEmailAsync(req, ct),
        };
}
```

(Use `services.AddKeyedSingleton<IEmailProvider>("resend", ...)` to
register multiple providers under different keys.)

## Pitfalls and gotchas

- **`From` must be on a verified domain.** Resend rejects sends from
  unverified domains. Verify your sending domain in the Resend
  console before pointing production at this adapter.
- **The adapter does not retry.** A 5xx response surfaces as
  `Success = false` and the caller decides what to do. Wire a Polly
  retry handler if you want automatic retry.
- **`Html` and `Text` should both be set.** Spam filters score
  higher on emails missing a plain-text alternative.
- **`To` is an array.** A single email goes in a one-element array.
  For bulk fan-out, pass the addresses or call once per recipient
  (Resend recommends per-recipient for personalisation).
- **API key is bearer-token, not basic-auth.** Don't paste it as a
  username; use the Authorization header.

## Internals (for the curious)

- The provider serialises `EmailRequest` directly via System.Text.Json.
  Property names match Resend's API casing — no extra attributes
  needed.
- The HTTP client's `BaseAddress` is set from `BaseUrl`; relative
  paths in `PostAsJsonAsync("emails", …)` resolve correctly.
- A 4xx response body typically contains `{ "name": "...",
  "message": "..." }` — the adapter returns the raw body so the
  caller can parse it if needed.

## Dependencies

| Reference                            | Used for                       |
|--------------------------------------|--------------------------------|
| `FireflyFramework.Notifications`     | `IEmailProvider`               |

`System.Net.Http.Json` (used for the REST call to Resend) ships in
the .NET framework — no package import needed.

## Java mapping

| .NET                     | Java                              |
|--------------------------|-----------------------------------|
| `ResendEmailProvider`    | `ResendEmailProvider`             |
| `ResendOptions`          | `ResendProperties`                |

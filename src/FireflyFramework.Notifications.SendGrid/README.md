# FireflyFramework.Notifications.SendGrid

## Overview

`FireflyFramework.Notifications.SendGrid` is the **SendGrid adapter**
for the framework's email port. It implements `IEmailProvider` (defined
in `FireflyFramework.Notifications`) by mapping the
provider-agnostic `EmailRequest` record onto SendGrid's
`SendGridMessage`, dispatching it through the official `SendGrid`
NuGet client, and translating the result back into `EmailResponse`.

The adapter is intentionally small — under 80 lines of
production code — because SendGrid's SDK already provides everything
heavy: HTTP transport, retries on transient failures, per-personalisation
recipient handling, attachment encoding helpers, multi-tenant API host
support. The adapter's job is purely to translate at the boundary and
turn HTTP-level success / failure into the framework's uniform
response shape.

This module mirrors
`org.fireflyframework:firefly-notifications-sendgrid`. The wire shape
is identical (SendGrid's REST API does not vary across SDKs), and the
options class — `SendGridOptions` — exposes the same `ApiKey` field
plus an additional `Host` override for tests, which the Java module
also has.

The adapter has two constructors: a production one that builds a real
`SendGridClient` from `IOptions<SendGridOptions>`, and a testing one
that accepts a pre-built `ISendGridClient`. The latter lets unit tests
substitute an NSubstitute mock and assert on the
`SendGridMessage` that the adapter constructs without ever speaking
HTTP.

## When to use this module

Pick SendGrid when:

- Your application already uses SendGrid for marketing or transactional
  email and you want a single SaaS provider.
- You need SendGrid-specific features such as dynamic templates,
  webhooks, suppressions, or sender authentication. (This adapter
  exposes the basic send path; the SDK is wired underneath, so you can
  also resolve `ISendGridClient` directly when you need the advanced
  features.)
- You want a managed reputation: SendGrid handles IP warming, DKIM,
  SPF, and bounces.

Pick a different adapter when:

- You want a smaller, simpler footprint (e.g. Resend's REST API has
  fewer moving parts).
- You're sending huge transactional volumes and want to self-operate
  via SMTP — the SDK is HTTP-only.
- Your provider isn't SendGrid (Postmark, Mailgun, Amazon SES, etc.).
  Implement `IEmailProvider` against their SDK; the adapter pattern
  is the same.

## Mental model

```
   application
       │
       │ IEmailProvider.SendEmailAsync(EmailRequest)
       ▼
 ┌─────────────────────────┐
 │ SendGridEmailProvider   │
 │   build SendGridMessage │
 │   AddTos / AddCcs /     │
 │     AddBccs             │
 │   AddAttachment(base64) │
 └────────────┬────────────┘
              │ ISendGridClient.SendEmailAsync
              ▼
       SendGrid REST API
              │
              ▼
   EmailResponse(MessageId = X-Message-Id, Success = 2xx)
```

`SendGridEmailProvider` does **not** retain state between calls. Every
send constructs a fresh `SendGridMessage`, populates it from the
`EmailRequest`, calls `ISendGridClient.SendEmailAsync`, and translates
the response. This makes the provider safe to register as a
singleton and to share across threads.

## Quick start

```json
{
  "Firefly": {
    "Notifications": {
      "SendGrid": {
        "ApiKey": "<sendgrid-api-key>"
      }
    }
  }
}
```

```csharp
using FireflyFramework.Notifications;
using FireflyFramework.Notifications.SendGrid;
using Microsoft.Extensions.DependencyInjection;

builder.Services.Configure<SendGridOptions>(
    builder.Configuration.GetSection(SendGridOptions.SectionName));
builder.Services.AddSingleton<IEmailProvider, SendGridEmailProvider>();

// Application call site:
public sealed class WelcomeEmailHandler
{
    private readonly IEmailProvider _emails;
    public WelcomeEmailHandler(IEmailProvider emails) => _emails = emails;

    public Task<EmailResponse> WelcomeAsync(string toAddress, CancellationToken ct = default) =>
        _emails.SendEmailAsync(new EmailRequest(
            From:    "no-reply@example.com",
            To:      new[] { toAddress },
            Cc:      null,
            Bcc:     null,
            Subject: "Welcome",
            Text:    "Welcome aboard.",
            Html:    "<p>Welcome aboard.</p>"), ct);
}
```

The provider is happy to be registered as a `Singleton` — internally
it holds one `ISendGridClient`, which is itself thread-safe.

## Public surface

| Type                       | Description                                                                |
|----------------------------|----------------------------------------------------------------------------|
| `SendGridOptions`          | Configuration record (`ApiKey`, optional `Host`).                          |
| `SendGridEmailProvider`    | `IEmailProvider` implementation. Two constructors (production / testing). |

`SendGridEmailProvider` constructors:

```csharp
// Production: builds a real SendGridClient from configuration.
public SendGridEmailProvider(IOptions<SendGridOptions> options);

// Testing: accepts an explicit ISendGridClient (NSubstitute mock, integration stub).
public SendGridEmailProvider(ISendGridClient client);
```

`SendGridOptions.SectionName` is the constant
`"Firefly:Notifications:SendGrid"`, used directly with
`Configuration.GetSection(SendGridOptions.SectionName)`.

## Configuration

| Option   | Type      | Default | Effect                                                                                                       |
|----------|-----------|---------|--------------------------------------------------------------------------------------------------------------|
| `ApiKey` | `string`  | empty   | Required. The SendGrid API key (`SG.xxxxx.yyyyy`). Must have at least the `mail.send` scope.                  |
| `Host`   | `string?` | `null`  | Optional override of the default `https://api.sendgrid.com` base URL. Set in tests to point at WireMock.     |

`appsettings.json` example:

```json
{
  "Firefly": {
    "Notifications": {
      "SendGrid": {
        "ApiKey": "SG.aaaaaaaaaaaa.bbbbbbbbbbbb"
      }
    }
  }
}
```

To inject the API key from a secrets manager, use the standard
`Microsoft.Extensions.Configuration` providers. The `[ApiKey]` field
binds case-insensitively from any environment variable spelled
`Firefly__Notifications__SendGrid__ApiKey`.

## Common patterns

### 1. Plain-text-only

```csharp
await provider.SendEmailAsync(new EmailRequest(
    From:    "ops@example.com",
    To:      new[] { "alerts@example.com" },
    Cc:      null, Bcc: null,
    Subject: "Disk usage 92%",
    Text:    "Disk usage on host-3 is 92%.",
    Html:    null));
```

### 2. HTML with attachment

```csharp
var pdf = await File.ReadAllBytesAsync("statement.pdf", ct);
await provider.SendEmailAsync(new EmailRequest(
    From:    "billing@example.com",
    To:      new[] { customer.Email },
    Cc:      null, Bcc: null,
    Subject: "Your statement",
    Text:    "Statement attached.",
    Html:    "<p>Your statement is attached.</p>",
    Attachments: new[]
    {
        new EmailAttachment("statement.pdf", "application/pdf", pdf),
    }));
```

The adapter base-64-encodes the bytes per SendGrid's wire format —
`SendGridEmailProviderTests.SendEmailAsync_AttachesBytes_AsBase64`
pins this contract.

### 3. CC and BCC

```csharp
await provider.SendEmailAsync(new EmailRequest(
    From:    "no-reply@example.com",
    To:      new[] { "alice@example.com" },
    Cc:      new[] { "carbon@example.com" },
    Bcc:     new[] { "audit@example.com" },
    Subject: "Welcome",
    Text:    "Welcome aboard.",
    Html:    null));
```

### 4. Test against a mocked `ISendGridClient`

The testing constructor lets you assert on the constructed
`SendGridMessage` without touching the network:

```csharp
using NSubstitute;
using SendGrid;

var client = Substitute.For<ISendGridClient>();
SendGridMessage? captured = null;
client.SendEmailAsync(Arg.Do<SendGridMessage>(m => captured = m), default)
      .Returns(new Response(HttpStatusCode.Accepted, new StringContent(""),
          new HttpResponseMessage(HttpStatusCode.Accepted)
          {
              Headers = { { "X-Message-Id", "msg-1" } },
          }.Headers));

var provider = new SendGridEmailProvider(client);
var resp = await provider.SendEmailAsync(NewRequest(), CancellationToken.None);

Assert.True(resp.Success);
Assert.Equal("msg-1", resp.MessageId);
Assert.Equal("Welcome", captured!.Subject);
```

This is the exact shape of `SendGridEmailProviderTests`.

### 5. Integration test against WireMock

The `Host` option exists so integration tests can stand up a fake
SendGrid endpoint and drive the real `SendGridClient` against it:

```csharp
var mock = WireMockServer.Start();
mock.Given(Request.Create().UsingPost().WithPath("/v3/mail/send"))
    .RespondWith(Response.Create().WithStatusCode(202)
        .WithHeader("X-Message-Id", "ok"));

var options = Options.Create(new SendGridOptions
{
    ApiKey = "test", Host = mock.Urls[0],
});
var provider = new SendGridEmailProvider(options);
```

## Pitfalls and gotchas

- **`Success` is purely 2xx-based.** A `202 Accepted` from SendGrid
  returns `Success = true`; a `400 Bad Request` returns
  `Success = false` with the response body in `ErrorMessage`. This
  matches SendGrid's documented contract: 2xx means "queued for
  delivery", not "delivered".

- **The `X-Message-Id` header is always populated on success.** If it
  isn't, you have likely received a synthetic 200 from a misconfigured
  middleware. The adapter does not validate this — it returns
  `MessageId` as whatever the header carried (or `null`).

- **`From` requires a verified sender.** SendGrid rejects mail from
  unverified senders with HTTP 403. The adapter surfaces the response
  body verbatim, so you'll see SendGrid's error message in
  `ErrorMessage`.

- **Don't mix the two constructors.** Decide whether the host wires up
  `IOptions<SendGridOptions>` (production / DI) or supplies an
  `ISendGridClient` (tests / shared client). The two are independent;
  one path or the other.

- **Attachments are bytes — never pre-encoded.** Passing a base-64
  string in `EmailAttachment.Data` will result in double-encoding and
  corrupted attachments. Pass the raw bytes; the adapter encodes for
  the wire.

  ```csharp
  // Bad
  new EmailAttachment("note.txt", "text/plain",
      Encoding.UTF8.GetBytes(Convert.ToBase64String(File.ReadAllBytes("note.txt"))))

  // Good
  new EmailAttachment("note.txt", "text/plain", File.ReadAllBytes("note.txt"))
  ```

## Internals (for the curious)

The constructor builds a `SendGridClient` that honours the optional
`Host` override:

```csharp
private static SendGridClient BuildClient(SendGridOptions opt) => opt.Host is null
    ? new SendGridClient(opt.ApiKey)
    : new SendGridClient(new SendGridClientOptions
      {
          ApiKey = opt.ApiKey,
          Host   = opt.Host,
      });
```

`SendEmailAsync` constructs the `SendGridMessage` field-by-field. The
`AddTos` / `AddCcs` / `AddBccs` calls are no-ops for empty
collections, so the `if (request.Cc?.Count > 0)` guard is purely for
clarity. Attachments are encoded once at the boundary:

```csharp
foreach (var att in request.Attachments)
{
    msg.AddAttachment(att.FileName, Convert.ToBase64String(att.Data), att.ContentType);
}
```

The response translation reads the `X-Message-Id` header from the
SDK's `Response.Headers` collection. If the response was unsuccessful,
the adapter reads the body asynchronously to populate `ErrorMessage`
— this avoids buffering the body when not needed.

```csharp
return new EmailResponse(
    response.Headers.FirstOrDefault(h => h.Key == "X-Message-Id").Value?.FirstOrDefault(),
    success,
    success ? null : await response.Body.ReadAsStringAsync(ct));
```

The provider is safe to share across threads: the SDK's
`SendGridClient` is documented to be thread-safe, and the adapter
holds no per-request state.

## Dependencies

| Reference                                  | Purpose                                            |
|--------------------------------------------|----------------------------------------------------|
| `FireflyFramework.Notifications` (project) | `IEmailProvider`, `EmailRequest`, `EmailResponse`, `EmailAttachment` |
| `Microsoft.Extensions.Options`             | `IOptions<SendGridOptions>` binding                |
| `SendGrid` (NuGet)                         | Official SendGrid SDK (`ISendGridClient`, `SendGridMessage`, `SendGridClient`, helpers) |

## Java mapping

| .NET                       | Java                                  |
|----------------------------|---------------------------------------|
| `SendGridEmailProvider`    | `SendGridEmailProvider`               |
| `SendGridOptions`          | `SendGridProperties`                  |
| `SendGridOptions.SectionName` | `firefly.notifications.sendgrid` (Spring property prefix) |

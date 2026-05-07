# FireflyFramework.Notifications.Twilio

## Overview

`FireflyFramework.Notifications.Twilio` is the **Twilio adapter** for
the framework's SMS port. It implements `ISmsProvider` (defined in
`FireflyFramework.Notifications`) on top of the official `Twilio`
.NET SDK, mapping the framework-level `SmsRequest` record to a
`CreateMessageOptions` payload, dispatching it through
`MessageResource.CreateAsync(options, client)`, and translating the
result back into `SmsResponse`.

The adapter wraps roughly 60 lines of code, but two design decisions
deserve flagging up front:

- The provider takes an explicit `ITwilioRestClient` rather than
  relying on Twilio's global `TwilioClient.Init(...)` static
  initialiser. Twilio's static `Init` mutates a single
  `TwilioRestClient.Instance`, which would cause unit tests to share
  state across cases. By accepting an explicit client, the
  adapter is unit-testable without spawning per-test isolation, and
  multi-tenant hosts can run two `TwilioSmsProvider`s side by side
  with different SIDs.

- All exceptions thrown by the SDK are caught and translated into
  `SmsResponse(null, false, ex.Message)`. This matches the contract
  in `FireflyFramework.Notifications`: delivery failures are reported
  in the response, not as exceptions. Programming errors (null
  arguments) still throw via `ArgumentNullException` in the constructor.

This module mirrors
`org.fireflyframework:firefly-notifications-twilio`. The wire shape
is identical (the underlying
`POST /2010-04-01/Accounts/{sid}/Messages.json` is invariant across
SDKs), and `TwilioOptions` exposes the same triple of `AccountSid`,
`AuthToken`, and `DefaultFromNumber` that the Java module's
`TwilioProperties` does.

## When to use this module

Pick Twilio when:

- You want global SMS delivery with broad carrier coverage (Twilio
  handles routing, opt-in compliance, and long-codes / short-codes).
- You're already using Twilio for voice or video and want a single
  provider for everything.
- You need MMS, WhatsApp via Twilio, or other Twilio-specific channels.
  (This adapter is SMS-only; for the others, resolve
  `ITwilioRestClient` directly.)

Stay away when:

- You're transmitting only OTPs. Specialised providers (Twilio Verify,
  Vonage Verify) often outperform plain SMS at lower cost. They have
  their own SDKs.
- You only need test fixtures — implement `ISmsProvider` directly with
  a `List<SmsRequest>` capture.

## Mental model

```
 application
     │
     │ ISmsProvider.SendSmsAsync(SmsRequest)
     ▼
 ┌────────────────────────────┐
 │ TwilioSmsProvider          │
 │   build CreateMessageOptions │
 │     To, From, Body         │
 │   try {                    │
 │     MessageResource        │
 │       .CreateAsync(opts,   │
 │                    client) │
 │   } catch ex {             │
 │     SmsResponse(false,     │
 │       ex.Message)          │
 │   }                        │
 └────────────┬───────────────┘
              │ ITwilioRestClient.RequestAsync
              ▼
       Twilio REST API
              │
              ▼
   SmsResponse(MessageId = message.Sid, Success)
```

The Twilio SDK ultimately calls `ITwilioRestClient.RequestAsync(Request)`
under the hood; `MessageResource.CreateAsync` is a typed shim. Tests
substitute a mock `ITwilioRestClient` and assert on the captured
`Request` (its `Method`, `Uri`, and `PostParams`).

## Quick start

```json
{
  "Firefly": {
    "Notifications": {
      "Twilio": {
        "AccountSid":         "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "AuthToken":          "<auth-token>",
        "DefaultFromNumber":  "+34000000000"
      }
    }
  }
}
```

```csharp
using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Twilio;
using Microsoft.Extensions.DependencyInjection;

builder.Services.Configure<TwilioOptions>(
    builder.Configuration.GetSection(TwilioOptions.SectionName));
builder.Services.AddSingleton<ISmsProvider, TwilioSmsProvider>();

// Application call site:
public sealed class OtpService
{
    private readonly ISmsProvider _sms;
    public OtpService(ISmsProvider sms) => _sms = sms;

    public Task<SmsResponse> SendCodeAsync(string e164, string code, CancellationToken ct = default) =>
        _sms.SendSmsAsync(new SmsRequest(
            PhoneNumber: e164,
            Message:     $"Your code is {code}",
            FromNumber:  null), ct);
}
```

The provider is happy to be registered as a `Singleton` — its
`ITwilioRestClient` is documented as thread-safe.

## Public surface

| Type                  | Description                                                                  |
|-----------------------|------------------------------------------------------------------------------|
| `TwilioOptions`       | Configuration record (`AccountSid`, `AuthToken`, `DefaultFromNumber`).       |
| `TwilioSmsProvider`   | `ISmsProvider` implementation. Two constructors (production / testing).      |

`TwilioSmsProvider` constructors:

```csharp
// Production: builds a TwilioRestClient from configured SID/auth-token.
public TwilioSmsProvider(IOptions<TwilioOptions> options);

// Testing / advanced: accepts an explicit ITwilioRestClient.
public TwilioSmsProvider(IOptions<TwilioOptions> options, ITwilioRestClient client);
```

The second overload exists so unit tests can drive the SDK against an
NSubstitute mock and so multi-tenant hosts can share or scope clients
explicitly. **Pass the client directly — do not call
`TwilioClient.Init(...)`.** The static initialiser mutates global
state and will leak across tests.

## Configuration

| Option              | Type     | Default | Effect                                                                                          |
|---------------------|----------|---------|-------------------------------------------------------------------------------------------------|
| `AccountSid`        | `string` | empty   | Required. The Twilio Account SID (`AC...`).                                                     |
| `AuthToken`         | `string` | empty   | Required. The Twilio Auth Token. Used to sign the API requests.                                 |
| `DefaultFromNumber` | `string` | empty   | The E.164 number to use when `SmsRequest.FromNumber` is `null`. Required to be a Twilio-verified number. |

`appsettings.json`:

```json
{
  "Firefly": {
    "Notifications": {
      "Twilio": {
        "AccountSid":        "AC0000000000000000000000000000",
        "AuthToken":         "deadbeefdeadbeefdeadbeefdeadbeef",
        "DefaultFromNumber": "+15551234567"
      }
    }
  }
}
```

For production, sensitive fields (`AuthToken`) should come from a
secrets manager (Azure Key Vault, AWS Secrets Manager, HashiCorp
Vault) wired into `Microsoft.Extensions.Configuration` rather than
checked into source control.

## Common patterns

### 1. Use the configured default sender number

```csharp
await provider.SendSmsAsync(new SmsRequest(
    PhoneNumber: "+15557654321",
    Message:     "Order #1234 has shipped.",
    FromNumber:  null));
```

The adapter substitutes `TwilioOptions.DefaultFromNumber` for the
`FromNumber` field.

### 2. Override the sender per call

For multi-brand hosts that vend SMS from different numbers per
tenant:

```csharp
await provider.SendSmsAsync(new SmsRequest(
    PhoneNumber: "+15557654321",
    Message:     "Welcome to Brand X",
    FromNumber:  "+15559999999"));
```

`TwilioSmsProviderTests.SendSmsAsync_HonoursPerRequest_FromNumber_OverDefault`
pins this behaviour — the per-request value beats the default.

### 3. Test against a mocked `ITwilioRestClient`

```csharp
using NSubstitute;
using Twilio.Clients;
using Twilio.Http;

var client = Substitute.For<ITwilioRestClient>();
client.AccountSid.Returns("AC0000000000000000000000000000");

Request? captured = null;
client.RequestAsync(Arg.Do<Request>(r => captured = r))
      .Returns(new Response(HttpStatusCode.Created,
          """{"sid":"SM123","to":"+15557654321","status":"queued"}"""));

var provider = new TwilioSmsProvider(Options.Create(new TwilioOptions
{
    AccountSid = "AC0000000000000000000000000000",
    AuthToken  = "auth",
    DefaultFromNumber = "+15551234567",
}), client);

var resp = await provider.SendSmsAsync(
    new SmsRequest("+15557654321", "ping", null), CancellationToken.None);

Assert.True(resp.Success);
Assert.Equal("SM123", resp.MessageId);
Assert.Equal(HttpMethod.Post, captured!.Method);
Assert.Contains("/Messages.json", captured.Uri.ToString());
Assert.Equal("+15557654321", captured.PostParams.First(p => p.Key == "To").Value);
```

### 4. Handle exceptions uniformly

Twilio's SDK throws on rate-limiting, invalid credentials, and a
handful of other categories. The adapter catches every exception type
the SDK can raise:

```csharp
try
{
    var message = await MessageResource.CreateAsync(options, _client);
    return new SmsResponse(message.Sid, true, null);
}
catch (Exception ex)
{
    return new SmsResponse(null, false, ex.Message);
}
```

So callers consume a uniform `SmsResponse` and never need a `try` /
`catch` of their own around the adapter.

## Pitfalls and gotchas

- **`ITwilioRestClient` is the boundary.** Anywhere the framework
  passes a `TwilioRestClient` (or mock thereof) you can assert on the
  captured `Request`. Twilio's `MessageResource.CreateAsync(options,
  client)` overload is what makes this work — the adapter intentionally
  uses it instead of `MessageResource.CreateAsync(options)` (which
  would consume the global static).

  ```csharp
  // Bad — uses the global TwilioClient.Instance set by TwilioClient.Init.
  await MessageResource.CreateAsync(options);

  // Good — explicit client; safe in tests and multi-tenant hosts.
  await MessageResource.CreateAsync(options, _client);
  ```

- **Phone numbers must be E.164.** `+34xxxxxxxxx`, not `0034...` or
  `34xxxxxxxxx`. Twilio rejects non-E.164 numbers with HTTP 400, which
  the adapter surfaces in `ErrorMessage`.

- **`DefaultFromNumber` must be a verified Twilio number.** Twilio
  rejects sends from numbers your account does not own. There is no
  pre-flight check in the adapter; the failure surfaces in
  `ErrorMessage`.

- **Message length.** Twilio splits messages over 160 GSM-7
  characters into multiple parts and bills accordingly. The adapter
  passes the body verbatim; if you want trimming or length validation,
  do it in your application code.

- **The constructor that takes `IOptions<TwilioOptions>` builds a real
  `TwilioRestClient` immediately.** That client opens an HTTP
  connection on first use; in tests you almost always want the
  two-argument overload that takes a substitute.

## Internals (for the curious)

The provider is intentionally small:

```csharp
public async Task<SmsResponse> SendSmsAsync(SmsRequest request, CancellationToken ct = default)
{
    try
    {
        var options = new CreateMessageOptions(new PhoneNumber(request.PhoneNumber))
        {
            From = new PhoneNumber(request.FromNumber ?? _opt.DefaultFromNumber),
            Body = request.Message,
        };

        var message = await MessageResource.CreateAsync(options, _client);
        return new SmsResponse(message.Sid, true, null);
    }
    catch (Exception ex)
    {
        return new SmsResponse(null, false, ex.Message);
    }
}
```

Three things are worth pointing out:

1. `CreateMessageOptions` carries the body, the `To` (positional in
   the constructor), and the `From`. Twilio also accepts
   `MessagingServiceSid` for messaging services rather than raw
   numbers; if your account uses one, sub-class the adapter or wire a
   custom call site through `ITwilioRestClient` directly.
2. `_client` is non-null in both constructors — the production path
   builds a real `TwilioRestClient(AccountSid, AuthToken)`, which is
   documented as thread-safe and connection-pooled.
3. The `try` / `catch (Exception ex)` is intentionally broad. The SDK
   raises a mix of `ApiException`, `RestException`, and core CLR
   exception types depending on the failure mode; rather than match
   each one, the adapter normalises every failure into
   `SmsResponse(null, false, ex.Message)`. The constructor still
   throws `ArgumentNullException` for null arguments because those are
   programming errors, not delivery failures.

## Dependencies

| Reference                                  | Purpose                                  |
|--------------------------------------------|------------------------------------------|
| `FireflyFramework.Notifications` (project) | `ISmsProvider`, `SmsRequest`, `SmsResponse` |
| `Microsoft.Extensions.Options`             | `IOptions<TwilioOptions>` binding        |
| `Twilio` (NuGet)                           | Official Twilio SDK (`ITwilioRestClient`, `TwilioRestClient`, `MessageResource`, `CreateMessageOptions`, `PhoneNumber`) |

## Java mapping

| .NET                  | Java                                |
|-----------------------|-------------------------------------|
| `TwilioSmsProvider`   | `TwilioSmsProvider`                 |
| `TwilioOptions`       | `TwilioProperties`                  |
| `TwilioOptions.SectionName` | `firefly.notifications.twilio` (Spring property prefix) |

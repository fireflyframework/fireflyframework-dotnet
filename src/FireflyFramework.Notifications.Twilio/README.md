# FireflyFramework.Notifications.Twilio

Twilio implementation of `ISmsProvider` using the official `Twilio` .NET
SDK and the `MessageResource.CreateAsync` API.

Mirrors `org.fireflyframework:firefly-notifications-twilio`.

## Configuration

```json
{
  "Firefly": {
    "Notifications": {
      "Twilio": {
        "AccountSid":         "<twilio account sid>",
        "AuthToken":          "<twilio auth token>",
        "DefaultFromNumber":  "+34000000000"
      }
    }
  }
}
```

## Wiring

```csharp
builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection(TwilioOptions.SectionName));
builder.Services.AddSingleton<ISmsProvider, TwilioSmsProvider>();
```

## Behaviour

- Calls `TwilioClient.Init(AccountSid, AuthToken)` once on construction.
- `SendSmsAsync` uses `request.FromNumber` if set, otherwise the
  configured `DefaultFromNumber`.
- Returns `SmsResponse(MessageId: message.Sid, Success: true)` on
  success; on exception returns `SmsResponse(null, false, ex.Message)`
  so the caller does not need to handle Twilio's exception types.

## Dependencies

| Reference                            | Used for             |
|--------------------------------------|----------------------|
| `FireflyFramework.Notifications`     | `ISmsProvider`       |
| `Twilio`                             | Twilio SDK           |

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `TwilioSmsProvider`   | `TwilioSmsProvider`               |
| `TwilioOptions`       | `TwilioProperties`                |

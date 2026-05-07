# FireflyFramework.Notifications.Resend

Resend (`https://resend.com`) implementation of `IEmailProvider`. Calls
the Resend REST API directly with bearer-token authentication.

Mirrors `org.fireflyframework:firefly-notifications-resend`.

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

## Wiring

```csharp
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.AddHttpClient<IEmailProvider, ResendEmailProvider>();
```

## Behaviour

- Posts the request to `POST {BaseUrl}/emails` with the API key as a
  `Bearer` token.
- Returns `EmailResponse(MessageId: response.id, Success: true)` on
  2xx; otherwise `EmailResponse(null, false, responseBody)`.

## Dependencies

| Reference                            | Used for                       |
|--------------------------------------|--------------------------------|
| `FireflyFramework.Notifications`     | `IEmailProvider`               |
| `System.Net.Http.Json`               | REST calls                     |

## Java mapping

| .NET                     | Java                              |
|--------------------------|-----------------------------------|
| `ResendEmailProvider`    | `ResendEmailProvider`             |
| `ResendOptions`          | `ResendProperties`                |

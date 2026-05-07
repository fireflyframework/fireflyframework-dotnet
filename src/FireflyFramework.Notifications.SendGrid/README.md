# FireflyFramework.Notifications.SendGrid

SendGrid implementation of `IEmailProvider`. Wraps the official
`SendGrid` NuGet client with the framework's `EmailRequest` /
`EmailResponse` shape, including CC / BCC and base-64 encoded
attachments.

Mirrors `org.fireflyframework:firefly-notifications-sendgrid`.

## Configuration

```json
{
  "Firefly": {
    "Notifications": {
      "SendGrid": {
        "ApiKey": "<sendgrid api key>"
      }
    }
  }
}
```

## Wiring

```csharp
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection(SendGridOptions.SectionName));
builder.Services.AddSingleton<IEmailProvider, SendGridEmailProvider>();
```

## Behaviour

- Maps `EmailRequest.{From,To,Cc,Bcc,Subject,Text,Html}` directly into
  `SendGridMessage`.
- Attaches every `EmailAttachment` as a base-64 encoded MIME part.
- Returns `EmailResponse(MessageId: header X-Message-Id, Success:
  IsSuccessStatusCode, ErrorMessage: response body when not successful)`.

## Dependencies

| Reference                            | Used for                       |
|--------------------------------------|--------------------------------|
| `FireflyFramework.Notifications`     | `IEmailProvider`               |
| `SendGrid`                           | SendGrid SDK                   |

## Java mapping

| .NET                       | Java                              |
|----------------------------|-----------------------------------|
| `SendGridEmailProvider`    | `SendGridEmailProvider`           |
| `SendGridOptions`          | `SendGridProperties`              |

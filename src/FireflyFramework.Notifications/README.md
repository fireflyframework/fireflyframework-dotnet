# FireflyFramework.Notifications

Provider-agnostic notification contracts and DTOs for email, SMS, and
push channels. Pure contract module with no implementation
dependencies.

Mirrors `org.fireflyframework:firefly-notifications`.

## Ports

```csharp
public interface IEmailProvider
{
    Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default);
}

public interface ISmsProvider
{
    Task<SmsResponse> SendSmsAsync(SmsRequest request, CancellationToken ct = default);
}

public interface IPushProvider
{
    Task<PushNotificationResponse> SendPushAsync(PushNotificationRequest request, CancellationToken ct = default);
}
```

## DTOs

| Type                                | Purpose                                                              |
|-------------------------------------|----------------------------------------------------------------------|
| `EmailRequest`                      | `From`, `To`, `Cc`, `Bcc`, `Subject`, `Text`, `Html`, `Attachments`  |
| `EmailResponse`                     | `MessageId`, `Success`, `ErrorMessage`                                |
| `EmailAttachment`                   | `FileName`, `ContentType`, `Data`                                    |
| `EmailTemplateRequest`              | Template id + variables + recipients                                  |
| `SmsRequest` / `SmsResponse`        | Phone number, message, optional source number                         |
| `PushNotificationRequest` / `Response` | Token, title, body, custom data dictionary                         |
| `EmailStatus`                       | `Pending`, `Sent`, `Failed`, `Bounced`                                |
| `NotificationPreferenceDto`         | Per-user channel toggles (`UserId`, `EmailEnabled`, `SmsEnabled`, `PushEnabled`) |

## Adapter projects

| Channel | Project                                          | Class                       |
|---------|--------------------------------------------------|-----------------------------|
| Email   | `FireflyFramework.Notifications.SendGrid`        | `SendGridEmailProvider`     |
| Email   | `FireflyFramework.Notifications.Resend`          | `ResendEmailProvider`       |
| SMS     | `FireflyFramework.Notifications.Twilio`          | `TwilioSmsProvider`         |
| Push    | `FireflyFramework.Notifications.Firebase`        | `FcmPushProvider`           |

## Service layer

`FireflyFramework.Notifications.Core` builds on top of these contracts
with `EmailService` / `SmsService` / `PushService` facades, a Scriban
template engine, and `NotificationDispatcher` that respects per-user
preferences.

## Dependencies

| Reference                                     | Used for                       |
|-----------------------------------------------|--------------------------------|
| `FireflyFramework.Kernel`                     | Calendar version               |

## Java mapping

| .NET               | Java                  |
|--------------------|-----------------------|
| `IEmailProvider`   | `EmailProvider`       |
| `ISmsProvider`     | `SmsProvider`         |
| `IPushProvider`    | `PushProvider`        |
| All DTOs           | matching DTO names    |

# FireflyFramework.Notifications

Provider-agnostic notification ports + DTOs. Mirrors `fireflyframework-notifications`.

## Contracts

```csharp
public interface IEmailProvider { Task<EmailResponse> SendEmailAsync(EmailRequest, CancellationToken); }
public interface ISmsProvider   { Task<SmsResponse>   SendSmsAsync(SmsRequest, CancellationToken); }
public interface IPushProvider  { Task<PushNotificationResponse> SendPushAsync(PushNotificationRequest, CancellationToken); }
```

## Adapters in this repo

| Provider | Project | Class |
|---|---|---|
| SendGrid (email) | `FireflyFramework.Notifications.SendGrid` | `SendGridEmailProvider` |
| Resend (email)   | `FireflyFramework.Notifications.Resend`   | `ResendEmailProvider` |
| Twilio (SMS)     | `FireflyFramework.Notifications.Twilio`   | `TwilioSmsProvider` |
| Firebase (push)  | `FireflyFramework.Notifications.Firebase` | `FcmPushProvider` |

## Application services

`FireflyFramework.Notifications.Core` adds `EmailService` / `SmsService` / `PushService` orchestrators plus `INotificationTemplateEngine` (default: `ScribanTemplateEngine`) for template rendering before send.

```csharp
var template = await templateEngine.RenderAsync("welcome", new() { ["name"] = "Alice" });
await emailService.SendAsync(new EmailRequest("from@example.com", new[] { "alice@example.com" }, null, null, "Welcome", null, template));
```

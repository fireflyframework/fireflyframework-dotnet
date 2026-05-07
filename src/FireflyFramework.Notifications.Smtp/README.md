# FireflyFramework.Notifications.Smtp

Plain-old SMTP email provider. Backed by `System.Net.Mail.SmtpClient`,
so it works against any RFC-5321 server (corporate Exchange relay,
Postfix, MailHog in tests, etc.).

The other notification adapters (SendGrid, Resend, Twilio, Firebase)
all assume a SaaS account. SMTP fills the gap pyfly already supports
where the deployment target only allows internal mail submission.

## Quick start

```csharp
services.AddSmtpEmailProvider(Configuration);
```

```yaml
Firefly:
  Notifications:
    Smtp:
      Host: smtp.internal.example.com
      Port: 587
      EnableSsl: true
      Username: notifications@example.com
      Password: ${SMTP_PASSWORD}
      DefaultFrom: notifications@example.com
      Timeout: 00:00:30
```

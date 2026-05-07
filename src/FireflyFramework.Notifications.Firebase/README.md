# FireflyFramework.Notifications.Firebase

Firebase Cloud Messaging implementation of `IPushProvider`. Wraps the
official `FirebaseAdmin` SDK to send push notifications to a single
device token.

Mirrors `org.fireflyframework:firefly-notifications-firebase`.

## Configuration

```json
{
  "Firefly": {
    "Notifications": {
      "Firebase": {
        "CredentialsPath": "/secrets/firebase-credentials.json",
        "ProjectId":       "my-firebase-project"
      }
    }
  }
}
```

If `CredentialsPath` is omitted the adapter falls back to
`GoogleCredential.GetApplicationDefault()`, so on Google-hosted
infrastructure no explicit credentials are needed.

## Wiring

```csharp
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.AddSingleton<IPushProvider, FcmPushProvider>();
```

## Behaviour

- Initialises the default `FirebaseApp` once if it has not been
  initialised yet.
- `SendPushAsync` builds an FCM `Message` (token, title, body,
  data dictionary) and posts it via `FirebaseMessaging.DefaultInstance.SendAsync`.
- Returns `PushNotificationResponse(MessageId: id, Success: true)` on
  success or a failure response containing the exception message.

## Dependencies

| Reference                            | Used for                       |
|--------------------------------------|--------------------------------|
| `FireflyFramework.Notifications`     | `IPushProvider`                |
| `FirebaseAdmin`                      | FCM SDK                        |
| `Google.Apis.Auth`                   | Application-default credentials |

## Java mapping

| .NET                | Java                              |
|---------------------|-----------------------------------|
| `FcmPushProvider`   | `FirebasePushProvider`            |
| `FirebaseOptions`   | `FirebaseProperties`              |

# FireflyFramework.Notifications.Firebase

## Overview

`FireflyFramework.Notifications.Firebase` is the **Firebase Cloud
Messaging (FCM) implementation of `IPushProvider`**. It wraps the
official `FirebaseAdmin` SDK to send push notifications to a single
device token.

Mirrors `org.fireflyframework:firefly-notifications-firebase`. The
framework's port surface (`IPushProvider.SendPushAsync`) is identical
across both runtimes, so a service that needs to call FCM from .NET
or Java has the same code shape.

## Why a separate module?

FCM is the dominant push provider for Android and (via APNs proxy)
iOS. The `FirebaseAdmin` SDK is sizeable and assumes a credentials
file or Application Default Credentials — neither belongs in the
generic notifications module. Splitting the adapter:

- Keeps FCM dependencies out of services that don't send push.
- Lets services that target other push providers (APNs directly,
  WNS) substitute their own implementation of `IPushProvider`.
- Mirrors the Java line's modular packaging.

## Mental model

```
   application code
        │
        │  IPushProvider.SendPushAsync(PushNotificationRequest)
        ▼
   ┌──────────────────────────┐
   │ FcmPushProvider          │
   └──────────┬───────────────┘
              │
              │  Build FCM Message:
              │  { token, title, body, data {} }
              ▼
   ┌──────────────────────────┐
   │ FirebaseMessaging        │  ← FirebaseAdmin SDK
   │ DefaultInstance.SendAsync│
   └──────────┬───────────────┘
              │
              │  HTTP/2 to fcm.googleapis.com
              ▼
   ┌──────────────────────────┐
   │  Firebase Cloud Messaging│
   │  routes to APNs / FCM    │
   └──────────────────────────┘
```

The adapter is initialised once per process — `FirebaseApp.Create()`
is global state, so the adapter checks before initialising to avoid
duplicate init exceptions.

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

| Property          | Notes                                                                  |
|-------------------|------------------------------------------------------------------------|
| `CredentialsPath` | Path to a service-account JSON file. Omit on Google-hosted infra to use ADC. |
| `ProjectId`       | Required when using ADC; redundant with explicit `CredentialsPath`     |

If `CredentialsPath` is omitted the adapter falls back to
`GoogleCredential.GetApplicationDefault()`, so on Google-hosted
infrastructure (GKE with workload identity, GCE, Cloud Run) no
explicit credentials are needed.

## Wiring

```csharp
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.AddSingleton<IPushProvider, FcmPushProvider>();
```

Singleton lifetime is correct — the underlying `FirebaseApp` and
`FirebaseMessaging.DefaultInstance` are global and thread-safe.

## Behaviour

- On first `SendPushAsync`, the adapter checks whether a default
  `FirebaseApp` exists; if not, it initialises one from the
  configured credentials.
- Builds an FCM `Message` with the target token, notification title,
  body, and a flat `data` dictionary for custom payload.
- Posts via `FirebaseMessaging.DefaultInstance.SendAsync(message)`.
- Returns `PushNotificationResponse(MessageId: id, Success: true)` on
  success, or a failure response containing the exception message.

## Common patterns

### Sending a notification with custom data

```csharp
var result = await push.SendPushAsync(new PushNotificationRequest
{
    DeviceToken = registrationToken,
    Title       = "New message from Ada",
    Body        = "Hi! Are you free for lunch?",
    Data        = new Dictionary<string, string>
    {
        ["conversationId"] = conversationId.ToString(),
        ["senderId"]       = adaUserId,
    },
}, ct);

if (!result.Success)
{
    log.LogWarning("FCM send failed: {Err}", result.ErrorMessage);
}
```

The `Data` dictionary is delivered to the client as a flat
key-value bundle. Use it for routing payloads (which screen to
open, which conversation to load) — *not* as a content carrier
(use `Body` for visible text).

### Multi-platform routing

For Android + iOS in one app, the same `Message` works for both —
APNs is reached transparently through FCM's APNs gateway. Make sure
the iOS configuration in your Firebase console uploads the APNs
key — without it, iOS deliveries silently fail.

### Handling an unregistered token

```csharp
var result = await push.SendPushAsync(req, ct);
if (!result.Success
    && result.ErrorMessage?.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase) == true)
{
    await tokens.MarkAsInvalidAsync(req.DeviceToken, ct);
}
```

FCM's "registration-token-not-registered" indicates the user
uninstalled the app or revoked notifications. Drop the token from
your store so subsequent sends don't waste FCM quota.

## Pitfalls and gotchas

- **`FirebaseApp` is global.** Don't try to initialise multiple
  `FirebaseApp` instances in the same process — it's fine to do so
  with named apps but the adapter uses the default instance. If you
  need named apps, fork the adapter.
- **Workload identity rules.** On GKE, the service account bound to
  the workload identity must have `roles/firebase.messaging.admin`
  (or scoped to FCM specifically). On GCE, the VM service account
  needs the same.
- **APNs needs setup in the console.** FCM-to-APNs requires an APNs
  key uploaded in the Firebase project settings. Without it,
  iOS sends silently fail with no obvious error.
- **`SendAsync` is single-token.** For multicast (>500 recipients),
  use `FirebaseMessaging.DefaultInstance.SendMulticastAsync` —
  extend the adapter if you need this.
- **Token quotas.** FCM enforces a per-project quota; bursting beyond
  quota returns a 429. Add a Polly retry-with-backoff handler if
  you fan out heavily.
- **Notification vs data messages.** Setting only `Data` (no
  notification block) sends a "data-only" message; the OS doesn't
  render anything until your app handles it. Setting `Title`/`Body`
  creates a system-rendered notification. Decide deliberately.

## Internals (for the curious)

- `FirebaseAdmin` initialises a single HTTP/2 connection pool for
  the project. The adapter benefits from connection reuse across
  sends.
- The adapter caches a `FirebaseMessaging.DefaultInstance` reference;
  the SDK keeps it process-scoped.
- `Application Default Credentials` chains: env var
  `GOOGLE_APPLICATION_CREDENTIALS` → gcloud user credentials →
  metadata server (GCE/GKE) → service-account creds when running on
  Compute Engine. The adapter doesn't override the chain — it uses
  whatever ADC resolves.

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

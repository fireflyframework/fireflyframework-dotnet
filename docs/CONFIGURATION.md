# Configuration Reference (`Firefly:*`)

> Every option recognised by Firefly Framework .NET, in the canonical
> `appsettings.json` namespace. Environment-variable form is the same
> path with `:` replaced by `__` (e.g. `Firefly:Web:Idempotency:Enabled`
> → `Firefly__Web__Idempotency__Enabled`).
>
> Each block below mirrors the matching `*Options` class under `src/`.
> Defaults shown are the values the options class ships with.

## Foundational

### `Firefly:Web`

```json
{
  "Firefly": {
    "Web": {
      "ErrorHandling": {
        "IncludeStackTrace":  false,
        "IncludeDebugInfo":   false,
        "ProblemTypeBaseUri": "https://errors.fireflyframework.org/",
        "MaskPii":            true
      },
      "Idempotency": {
        "Enabled":      true,
        "HeaderName":   "X-Idempotency-Key",
        "Ttl":          "24:00:00",
        "MaxKeyLength": 256,
        "Methods":      ["POST", "PATCH", "PUT", "DELETE"]
      },
      "PiiMasking": {
        "Enabled":          true,
        "MaskCharacter":    "*",
        "VisiblePrefix":    2,
        "VisibleSuffix":    2,
        "SensitiveFields":  ["password", "secret", "token", "apiKey", "authorization",
                             "ssn", "creditCard", "cardNumber", "cvv", "iban", "pin"],
        "SensitivePatterns": []
      },
      "Cors": {
        "Enabled":          true,
        "AllowedOrigins":   ["*"],
        "AllowedMethods":   ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
        "AllowedHeaders":   ["*"],
        "ExposedHeaders":   ["X-Correlation-Id", "X-Request-Id", "X-Idempotency-Key"],
        "AllowCredentials": false,
        "PreflightMaxAge":  "00:10:00"
      }
    }
  }
}
```

## Platform

### `Firefly:Cache`

```json
{
  "Firefly": {
    "Cache": {
      "Provider":  "Memory",                              // Memory | Redis | NoOp | Auto
      "Name":      "default",
      "KeyPrefix": "firefly:cache:",
      "Memory":  { "SizeLimit": 100000 },
      "Redis":   { "ConnectionString": "localhost:6379", "DefaultTtl": "00:05:00" }
    }
  }
}
```

### `Firefly:Observability`

```json
{
  "Firefly": {
    "Observability": {
      "Metrics":  {
        "Enabled":      true,
        "Prefix":       "firefly",
        "Exporter":     "Both",                           // Prometheus | Otlp | Both
        "OtlpEndpoint": "http://otel-collector:4317"
      },
      "Tracing":  {
        "Enabled":              true,
        "Bridge":               "OpenTelemetry",          // OpenTelemetry | Brave
        "SamplingProbability":  1.0,
        "Propagation":          "W3C",                    // W3C | B3
        "BaggageFields":        ["tenant-id", "correlation-id"],
        "OtlpEndpoint":         "http://otel-collector:4317"
      },
      "Health":   { "Enabled": true, "KubernetesProbes": true },
      "Logging":  { "Enabled": true, "StructuredFormat": true }
    }
  }
}
```

### `Firefly:Eda`

```json
{
  "Firefly": {
    "Eda": {
      "DefaultPublisher": "Kafka",                        // Kafka | RabbitMq | InMemory | Auto | Noop
      "DefaultConsumer":  "Kafka",                        // Kafka | RabbitMq | InMemory | Auto | Noop
      "Kafka": {
        "BootstrapServers":  "localhost:9092",
        "GroupId":           "orders-consumer",
        "SchemaRegistryUrl": "http://localhost:8081"
      },
      "RabbitMq": {
        "Hostname":    "localhost",
        "Port":        5672,
        "Username":    "guest",
        "Password":    "guest",
        "VirtualHost": "/"
      }
    }
  }
}
```

`Auto` resolves to `Kafka` when the Kafka client is available, otherwise
falls back to `InMemory`. Use `Noop` to disable publishing in tests.

## Adapters

### `Firefly:Idp:Keycloak`

Bound when you register `KeycloakIdpAdapter` (or `KeycloakAdminClient`).

```json
{
  "Firefly": {
    "Idp": {
      "Keycloak": {
        "ServerUrl":            "http://localhost:8080",
        "Realm":                "master",
        "ClientId":             "orders-service",
        "ClientSecret":         "***",
        "AdminUsername":        "admin",
        "AdminPassword":        "***",
        "VerifyTokenSignature": true
      }
    }
  }
}
```

### `Firefly:Idp:Cognito`

Bound when you register `CognitoIdpAdapter`.

```json
{
  "Firefly": {
    "Idp": {
      "Cognito": {
        "Region":       "eu-west-1",
        "UserPoolId":   "eu-west-1_xxxxxxxxx",
        "ClientId":     "...",
        "ClientSecret": "***"
      }
    }
  }
}
```

### `Firefly:Idp:AzureAd`

Bound when you register `AzureAdIdpAdapter`. Properties match the
`AzureAdOptions` class — `Authority`, `TenantId`, `ClientId`,
`ClientSecret`, `Audience`, `Scopes`.

### `Firefly:Idp:InternalDb`

Bound when you register `InternalDbIdpAdapter`. Properties match the
`InternalDbOptions` class — JWT signing key, issuer, audience, lifetime,
and BCrypt cost factor.

### `Firefly:Ecm`

The ECM module exposes adapter-specific configuration sections rather
than a single provider switch. Register exactly one adapter per port
and bind its options under `Firefly:Ecm:<AdapterName>`:

| Section                       | Adapter                               |
|-------------------------------|---------------------------------------|
| `Firefly:Ecm:Storage:Aws`     | `S3DocumentContentAdapter`            |
| `Firefly:Ecm:Storage:Azure`   | `AzureBlobDocumentContentAdapter`     |
| `Firefly:Ecm:ESignature:DocuSign`  | `DocuSignSignatureAdapter`       |
| `Firefly:Ecm:ESignature:AdobeSign` | `AdobeSignSignatureAdapter`      |
| `Firefly:Ecm:ESignature:Logalty`   | `LogaltySignatureAdapter`        |

Each adapter's options class lives next to its implementation under
`src/FireflyFramework.Ecm.*/`. See the per-module README for the full
property list.

### `Firefly:Notifications`

```json
{
  "Firefly": {
    "Notifications": {
      "SendGrid": { "ApiKey": "***", "From": "no-reply@example.com" },
      "Resend":   { "ApiKey": "***", "From": "no-reply@example.com" },
      "Twilio":   { "AccountSid": "...", "AuthToken": "***", "From": "+34..." },
      "Firebase": { "ServiceAccountKey": "/secrets/firebase-admin.json" }
    }
  }
}
```

The dispatcher (`FireflyFramework.Notifications.Core`) selects the
provider per channel based on the registered `IEmailProvider`,
`ISmsProvider`, and `IPushProvider`. Per-user preferences are
persisted via `INotificationPreferenceStore`.

### `Firefly:Callbacks`

`Firefly:Callbacks` configures the **outbound** callback subsystem
(HMAC signing, retry, audit). Per-callback configuration is persisted
through `ICallbackConfigurationStore`, not in `appsettings.json`. The
only static option that exists today is the dispatcher-wide HTTP
timeout:

```json
{
  "Firefly": {
    "Callbacks": {
      "HttpTimeoutSeconds": 30
    }
  }
}
```

### `Firefly:Webhooks`

`Firefly:Webhooks` configures the **inbound** webhook ingestion
pipeline. Bind the retry and rate-limit options under their sub-keys
(this matches the `WebhookOptions` class in
`FireflyFramework.Webhooks.Core`).

```json
{
  "Firefly": {
    "Webhooks": {
      "Retry": {
        "MaxAttempts":       3,
        "InitialDelayMs":    200,
        "MaxDelayMs":        30000,
        "BackoffMultiplier": 2.0
      },
      "RateLimit": {
        "RequestsPerSecond": 100,
        "BurstSize":         200
      }
    }
  }
}
```

Provider-specific signing secrets are passed through the validator
options of each `IWebhookSignatureValidator`. Compression, batching,
and DLQ behaviours are configured at registration time (the
`WebhookCompressionService`, `WebhookBatchingService`, and
`IWebhookDeadLetterStore` services), not through `appsettings.json`.

### `Firefly:ConfigServer`

```json
{
  "Firefly": {
    "ConfigServer": {
      "Enabled":  true,
      "Uri":      "http://config:8888",
      "Name":     "orders-service",
      "Profile":  "Production",
      "Label":    "main",
      "FailFast": true
    }
  }
}
```

### `Firefly:Client`

`FireflyFramework.Client` does not register a service-discovery layer
of its own. Use `IHttpClientFactory` named clients with
`Microsoft.Extensions.ServiceDiscovery` (already pinned in CPM) to
resolve service base addresses, and configure each client through the
existing `services.AddHttpClient(...)` chain.

## Validation

Most options classes are bound via the `Options` pattern. To fail fast
at startup on misconfiguration, the host can call:

```csharp
services.AddOptions<FireflyCoreOptions>()
    .BindConfiguration("Firefly:Core")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

This is opt-in per options type — there is no global "validate all"
switch.

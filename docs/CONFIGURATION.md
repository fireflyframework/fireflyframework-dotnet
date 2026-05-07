# Configuration Reference (`Firefly:*`)

> Every option recognised by Firefly Framework .NET, in the canonical
> `appsettings.json` namespace. Environment-variable form is the same
> path with `:` replaced by `__` (e.g. `Firefly:Web:ProblemDetails:Enabled`
> → `Firefly__Web__ProblemDetails__Enabled`).

## Foundational

### `Firefly:Web`

```json
{
  "Firefly": {
    "Web": {
      "ProblemDetails":  { "Enabled": true, "Source": "firefly", "TitlePrefix": "" },
      "CorrelationId":   { "Header": "X-Correlation-Id", "Generate": true, "PropagateOutbound": true },
      "Idempotency":     { "Enabled": true, "Header": "Idempotency-Key", "TtlSeconds": 600 },
      "Cors":            { "AllowedOrigins": ["*"], "AllowedMethods": ["GET", "POST"] },
      "RateLimiting":    { "Enabled": true, "PermitLimit": 100, "WindowSeconds": 60 }
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
      "DefaultProvider": "Memory",
      "Memory":  { "SizeLimit": 100000 },
      "Redis":   { "ConnectionString": "localhost:6379", "InstanceName": "firefly:" },
      "DefaultTtlSeconds": 300
    }
  }
}
```

### `Firefly:Observability`

```json
{
  "Firefly": {
    "Observability": {
      "Otel": {
        "Enabled": true,
        "ServiceName": "orders-service",
        "OtlpEndpoint": "http://otel-collector:4317",
        "Tracing":  { "Enabled": true, "SamplingProbability": 1.0 },
        "Metrics":  { "Enabled": true, "PrometheusExporter": true },
        "Logs":     { "Enabled": true }
      }
    }
  }
}
```

### `Firefly:Data`

```json
{
  "Firefly": {
    "Data": {
      "Provider":          "Postgres",            // InMemory | Postgres | SqlServer
      "ConnectionString":  "Host=db;Port=5432;Database=orders;Username=app;Password=***",
      "MigrateOnStartup":  true,
      "EnableSensitiveDataLogging": false
    }
  }
}
```

### `Firefly:Cqrs`

```json
{
  "Firefly": {
    "Cqrs": {
      "AutoDiscover": true,
      "ScanAssemblies": ["Orders.Application", "Orders.Domain"],
      "Behaviors":     ["Logging", "Validation", "Idempotency"],
      "Authorization": { "Enabled": false }
    }
  }
}
```

### `Firefly:Eda`

```json
{
  "Firefly": {
    "Eda": {
      "Provider":  "Kafka",                       // Kafka | RabbitMq | InMemory
      "Kafka": {
        "BootstrapServers": "localhost:9092",
        "ClientId":         "orders-service",
        "GroupId":          "orders-consumer",
        "SchemaRegistryUrl": "http://localhost:8081",
        "EnableManualCommit": true,
        "Topics":           { "OrderEvents": "orders.v1" }
      },
      "RabbitMq": {
        "HostName": "localhost",
        "Port": 5672,
        "UserName": "guest",
        "Password": "guest",
        "VirtualHost": "/",
        "Exchanges":  { "OrderEvents": "orders.exchange" }
      }
    }
  }
}
```

### `Firefly:EventSourcing`

```json
{
  "Firefly": {
    "EventSourcing": {
      "Provider":           "Postgres",           // InMemory | Postgres
      "SnapshotEvery":      50,
      "OutboxEnabled":      true,
      "OutboxIntervalMs":   1000,
      "ProjectionsEnabled": true,
      "Upcasters":          ["Orders.Upcasters.OrderCreatedV1ToV2"]
    }
  }
}
```

### `Firefly:Orchestration`

```json
{
  "Firefly": {
    "Orchestration": {
      "Saga":     { "Enabled": true,  "TimeoutSeconds": 300 },
      "Workflow": { "Enabled": true,  "Persistence": "Postgres" },
      "Tcc":      { "Enabled": false, "TimeoutSeconds": 30 }
    }
  }
}
```

### `Firefly:RuleEngine`

```json
{
  "Firefly": {
    "RuleEngine": {
      "Enabled":  true,
      "Source":   "File",                         // File | Database | ConfigServer
      "FilePath": "rules/order-rules.yml",
      "ReloadSeconds": 30
    }
  }
}
```

### `Firefly:Plugins`

```json
{
  "Firefly": {
    "Plugins": {
      "Directory":   "./plugins",
      "AutoLoad":    true,
      "HotReload":   true,
      "ManifestFile": "plugin.json"
    }
  }
}
```

## Adapters

### `Firefly:Idp`

```json
{
  "Firefly": {
    "Idp": {
      "Provider": "AzureAd",                      // Keycloak | Auth0 | AzureAd | Cognito
      "Keycloak": { "Authority": "https://kc.example.com/realms/myrealm", "Audience": "orders" },
      "Auth0":    { "Domain": "tenant.eu.auth0.com", "Audience": "orders", "ClientId": "...", "ClientSecret": "..." },
      "AzureAd":  { "TenantId": "...", "ClientId": "...", "ClientSecret": "...", "Audience": "api://orders" },
      "Cognito":  { "Region": "eu-west-1", "UserPoolId": "...", "ClientId": "..." }
    }
  }
}
```

### `Firefly:Ecm`

```json
{
  "Firefly": {
    "Ecm": {
      "Provider": "Sharepoint",                   // Sharepoint | OneDrive | Box | GoogleDrive | Drupal
      "Sharepoint": { "TenantId": "...", "ClientId": "...", "ClientSecret": "...", "SiteId": "..." },
      "OneDrive":   { "TenantId": "...", "ClientId": "...", "ClientSecret": "...", "DriveId": "..." },
      "Box":        { "ClientId": "...", "ClientSecret": "...", "EnterpriseId": "..." },
      "GoogleDrive":{ "ServiceAccountKey": "/secrets/google-drive.json" },
      "Drupal":     { "BaseUrl": "https://drupal.example.com", "Token": "..." }
    }
  }
}
```

### `Firefly:Notifications`

```json
{
  "Firefly": {
    "Notifications": {
      "Email": { "Provider": "SendGrid", "ApiKey": "***", "From": "no-reply@example.com" },
      "Sms":   { "Provider": "Twilio",   "AccountSid": "...", "AuthToken": "...", "From": "+34..." },
      "Push":  { "Provider": "Fcm",      "ServerKey": "..." },
      "Slack": { "WebhookUrl": "https://hooks.slack.com/..." },
      "Webhook": { "DefaultRetries": 3, "TimeoutSeconds": 30 }
    }
  }
}
```

### `Firefly:Callbacks`

```json
{
  "Firefly": {
    "Callbacks": {
      "DocuSign": {
        "BaseUrl":     "https://demo.docusign.net/restapi",
        "AccountId":   "...",
        "IntegrationKey": "...",
        "UserId":      "...",
        "RsaPrivateKey": "/secrets/docusign.pem"
      },
      "AdobeSign": { "ClientId": "...", "ClientSecret": "...", "RedirectUri": "..." },
      "Twilio":    { "AccountSid": "...", "AuthToken": "...", "FromPhone": "+34..." },
      "Vonage":    { "ApiKey": "...", "ApiSecret": "..." },
      "Calendar":  { "Provider": "Google", "ServiceAccountKey": "/secrets/google-cal.json" }
    }
  }
}
```

### `Firefly:Webhooks`

```json
{
  "Firefly": {
    "Webhooks": {
      "Receivers": {
        "Stripe": { "SignatureSecret": "whsec_***", "ToleranceSeconds": 300 },
        "GitHub": { "Secret": "***" },
        "Twilio": { "AuthToken": "***", "Url": "https://my.app/twilio" },
        "Generic": { "HmacAlgorithm": "Sha256", "Header": "X-Signature", "Secret": "***" }
      },
      "Security": {
        "MaxPayloadBytes": 1048576,
        "AllowedCidrs":    ["10.0.0.0/8", "192.168.1.0/24"],
        "BlockedCidrs":    [],
        "RequireTls":      true
      },
      "RateLimit": { "PermitLimit": 1000, "WindowSeconds": 60, "Backend": "Redis" },
      "Compression":  { "Enabled": true, "MinBytes": 1024 },
      "Batching":     { "Enabled": true, "MaxSize": 100, "FlushMs": 250 },
      "DeadLetter":   { "Enabled": true, "Backend": "Memory", "RedeliverAfterSeconds": 60 }
    }
  }
}
```

### `Firefly:ConfigServer`

```json
{
  "Firefly": {
    "ConfigServer": {
      "Enabled": true,
      "Uri":     "http://config:8888",
      "Name":    "orders-service",
      "Profile": "Production",
      "Label":   "main",
      "FailFast": true
    }
  }
}
```

### `Firefly:Client`

```json
{
  "Firefly": {
    "Client": {
      "Rest": {
        "Defaults": { "TimeoutSeconds": 30, "RetryAttempts": 3, "CircuitBreakerEnabled": true },
        "Endpoints": {
          "Orders":   { "BaseUrl": "https://orders.svc.local",   "TimeoutSeconds": 10 },
          "Payments": { "BaseUrl": "https://payments.svc.local", "TimeoutSeconds": 5 }
        }
      },
      "Soap": {
        "Endpoints": { "Legacy": { "Address": "https://legacy/soap", "Username": "...", "Password": "..." } }
      },
      "WebSocket": {
        "Endpoints": { "MarketData": { "Url": "wss://md.svc.local/stream" } }
      }
    }
  }
}
```

## Environment-variable shortcuts

The most-frequently overridden values support a short form (mirroring
the Java `firefly.*` aliases):

| Shortcut env var                  | Maps to                                                   |
|-----------------------------------|-----------------------------------------------------------|
| `FIREFLY_ENV`                     | `ASPNETCORE_ENVIRONMENT`                                  |
| `FIREFLY_DB_URL`                  | `Firefly:Data:ConnectionString`                           |
| `FIREFLY_KAFKA_BROKERS`           | `Firefly:Eda:Kafka:BootstrapServers`                      |
| `FIREFLY_REDIS_URL`               | `Firefly:Cache:Redis:ConnectionString`                    |
| `FIREFLY_OTEL_ENDPOINT`           | `Firefly:Observability:Otel:OtlpEndpoint`                 |

## Validation

Every options class is annotated with DataAnnotations *and* registered
through `services.AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`,
so a misconfigured service fails fast at startup rather than at the
first request.

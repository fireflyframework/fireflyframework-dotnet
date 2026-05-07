# Module Catalogue

> One-line description of every project in `src/` plus its Java original.
> Cross-reference for [ARCHITECTURE.md](ARCHITECTURE.md) and
> [MIGRATION-GUIDE.md](MIGRATION-GUIDE.md).

## 01 — Foundational

| .NET project                                 | Java original                           | Purpose                                                                       |
|----------------------------------------------|-----------------------------------------|-------------------------------------------------------------------------------|
| `FireflyFramework.Kernel`                    | `firefly-common`                        | RFC 7807 ProblemDetails, OperationResult<T>, IClock, FireflyException         |
| `FireflyFramework.Utils`                     | `firefly-common-utils`                  | Slug, crypto, Try.Of, RetryUtils, Templating helpers                          |
| `FireflyFramework.Validators`                | `firefly-common-validators`             | Country, IBAN, VAT, phone, e-mail validators                                  |
| `FireflyFramework.Web`                       | `firefly-web` + `firefly-spring-utils`  | Problem-Details middleware, correlation-id, idempotency, rate-limit           |

## 02 — Platform

| .NET project                                 | Java original                                   | Purpose                                                                |
|----------------------------------------------|-------------------------------------------------|------------------------------------------------------------------------|
| `FireflyFramework.Cache`                     | `firefly-common-cache`                          | StackExchange.Redis + IMemoryCache port abstraction                    |
| `FireflyFramework.Observability`             | `firefly-otel-spring-boot-starter`              | OpenTelemetry .NET tracing/metrics/logs configuration                  |
| `FireflyFramework.Data`                      | `firefly-common-data` (R2DBC)                   | EF Core 9 + filter / pagination / repository helpers                   |
| `FireflyFramework.Cqrs`                      | `firefly-common-cqrs`                           | Command / query bus + handler discovery + behaviours                   |
| `FireflyFramework.Eda`                       | `firefly-common-eda`                            | Kafka + Schema Registry + RabbitMQ producer / consumer                 |
| `FireflyFramework.EventSourcing`             | `firefly-event-sourcing-spring-boot-starter`    | Aggregates, snapshots, projections, outbox, upcasters                  |
| `FireflyFramework.Orchestration`             | `firefly-common-domain` (orchestration)         | Saga, workflow engine, TCC                                             |
| `FireflyFramework.RuleEngine.{Interfaces,Models,Core,Web,Sdk}` | `firefly-common-rule-engine`   | YAML DSL → in-memory rule graph                                        |
| `FireflyFramework.Plugins.{Api,Core}`        | `firefly-platform-plugins`                      | Plugin lifecycle, hot-reload via McMaster.NETCore.Plugins              |

## 03 — Adapters

### IDP

| .NET project                          | Java original                       | Backing tech                            |
|---------------------------------------|-------------------------------------|-----------------------------------------|
| `FireflyFramework.Idp`                | `firefly-idp`                       | Common IDP port + abstractions          |
| `FireflyFramework.Idp.InternalDb`     | `firefly-idp-internal-db`           | EF Core users + BCrypt + JWT            |
| `FireflyFramework.Idp.Keycloak`       | `firefly-idp-keycloak`              | Keycloak.AuthServices                   |
| `FireflyFramework.Idp.AzureAd`        | `firefly-idp-azure-ad`              | Microsoft.Graph + Azure.Identity        |
| `FireflyFramework.Idp.AwsCognito`     | `firefly-idp-aws-cognito`           | AWSSDK.CognitoIdentityProvider          |

### ECM (storage + e-signature)

| .NET project                                  | Java original                              |
|-----------------------------------------------|--------------------------------------------|
| `FireflyFramework.Ecm`                        | `firefly-ecm`                              |
| `FireflyFramework.Ecm.Storage.Aws`            | `firefly-ecm-storage-aws`                  |
| `FireflyFramework.Ecm.Storage.Azure`          | `firefly-ecm-storage-azure`                |
| `FireflyFramework.Ecm.ESignature.AdobeSign`   | `firefly-ecm-esignature-adobe-sign`        |
| `FireflyFramework.Ecm.ESignature.DocuSign`    | `firefly-ecm-esignature-docusign`          |
| `FireflyFramework.Ecm.ESignature.Logalty`     | `firefly-ecm-esignature-logalty`           |

### Notifications

| .NET project                              | Java original                          | Channel  |
|-------------------------------------------|----------------------------------------|----------|
| `FireflyFramework.Notifications`          | `firefly-notifications`                | Contract DTOs |
| `FireflyFramework.Notifications.Core`     | `firefly-notifications` (services)     | Dispatcher + template engine |
| `FireflyFramework.Notifications.SendGrid` | `firefly-notifications-sendgrid`       | Email    |
| `FireflyFramework.Notifications.Resend`   | `firefly-notifications-resend`         | Email    |
| `FireflyFramework.Notifications.Twilio`   | `firefly-notifications-twilio`         | SMS      |
| `FireflyFramework.Notifications.Firebase` | `firefly-notifications-firebase`       | Push     |

### Callbacks (e-signing + voice)

| .NET project                          | Java original                |
|---------------------------------------|------------------------------|
| `FireflyFramework.Callbacks.Interfaces` | `firefly-callbacks`        |
| `FireflyFramework.Callbacks.Models`   | "                            |
| `FireflyFramework.Callbacks.Core`     | "                            |
| `FireflyFramework.Callbacks.Sdk`      | "                            |
| `FireflyFramework.Callbacks.Web`      | "                            |

### Webhooks

| .NET project                                 | Purpose                                                          |
|----------------------------------------------|------------------------------------------------------------------|
| `FireflyFramework.Webhooks.Interfaces`       | DTOs / contract                                                  |
| `FireflyFramework.Webhooks.Core`             | Compression, batching, rate-limit, DLQ, validator, enrichment    |
| `FireflyFramework.Webhooks.Processor`        | Stripe / GitHub / Twilio / generic HMAC signature validators     |
| `FireflyFramework.Webhooks.Sdk`              | Provider-side helpers                                            |
| `FireflyFramework.Webhooks.Web`              | Inbound endpoint scaffolding                                     |

### Cross-protocol clients

| .NET project                          | Java original                 | Coverage                                |
|---------------------------------------|-------------------------------|-----------------------------------------|
| `FireflyFramework.Client`             | `firefly-service-client`      | REST builder + SOAP + WebSocket + gRPC  |
| `FireflyFramework.ConfigServer`       | `firefly-config-server`       | Spring-Cloud-Config-compatible server   |

## 04 — Starters

| .NET project                                  | Bundles                                                      |
|-----------------------------------------------|--------------------------------------------------------------|
| `FireflyFramework.Starter.Core`               | Web + Cache + Observability + EDA + CQRS                     |
| `FireflyFramework.Starter.Application`        | Core + Plugins + IDP + Orchestration                         |
| `FireflyFramework.Starter.Domain`             | Core + EventSourcing                                         |
| `FireflyFramework.Starter.Data`               | Core + EF Core + Polly                                       |
| `FireflyFramework.BackOffice`                 | Application + back-office context resolver / middleware      |

## 05 — Tests

`tests/FireflyFramework.Tests/` is a single xUnit project covering every tier
(currently 133 tests).

## 06 — Samples

`samples/FireflyFramework.Samples.OrdersService/` — end-to-end showcase using
the Application starter, EF Core, EDA, and event sourcing.

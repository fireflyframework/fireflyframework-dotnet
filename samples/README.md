# Samples

End-to-end reference services that demonstrate how to build a microservice
on top of FireflyFramework.NET. Each sample follows the canonical
**five-project layout** that mirrors the Java multi-module Maven structure
used by every Firefly platform service.

## Canonical layout

| Suffix         | Role                                                                              | Java analogue            |
|----------------|-----------------------------------------------------------------------------------|--------------------------|
| `.Interfaces`  | Public DTOs, request/response records, enums. No business logic, no I/O.          | `*-interfaces`           |
| `.Models`      | Persistence entities + repository contracts. Replace in-memory store with EF Core.| `*-models`               |
| `.Core`        | Commands, queries, handlers, mappers, business services. Holds the rules.         | `*-core`                 |
| `.Web`         | Runnable ASP.NET Core 9 host: `Program.cs`, controllers / minimal-API endpoints.  | `*-web` (Spring Boot)    |
| `.Sdk`         | Typed `HttpClient` for other services to call this one in-process.                | `*-sdk` (codegen target) |

The dependency graph is strictly layered:

```
Interfaces ◄── Models ◄── Core ◄── Web
   ▲
   └────── Sdk
```

`Sdk` consumes only `Interfaces` so external callers never pull in your
persistence layer or business logic.

## Available samples

| Sample              | Demonstrates                                              |
|---------------------|-----------------------------------------------------------|
| `OrdersService`     | CQRS (command + query + cache) · idempotency · OpenAPI · Sdk |

See [`docs/SERVICE-SCAFFOLDING.md`](../docs/SERVICE-SCAFFOLDING.md) for the
full template and the rationale behind each project boundary.

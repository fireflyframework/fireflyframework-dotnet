# Samples

## Overview

The `samples/` folder ships **runnable reference services** that
demonstrate how to build a microservice on top of FireflyFramework.NET.
Each sample follows the canonical **five-project layout** that mirrors
the Java multi-module Maven structure used by every Firefly platform
service. Reading the samples is the fastest way to understand how the
abstractions compose in practice — the README in each project
explains *what* it does, the source shows *how*.

Treat the samples as templates. The recommended way to start a new
service is:

1. Copy the `OrdersService` directory tree.
2. Rename the projects (e.g. `Payments` instead of `OrdersService`).
3. Rename the namespaces and assembly names accordingly.
4. Replace the example DTOs, entities, commands, queries, and
   handlers with your own.

The framework code never changes — your service code does.

## Why five projects, not one?

A microservice that fits in one project is fine until the day a
sister service wants to call it. At that point you face a choice:

- **Hand-write a typed HTTP client.** Wire format drift is now your
  problem.
- **Generate one from OpenAPI.** Codegen drift is now your problem.
- **Or share the DTOs in a separate assembly** so the call-site
  compiles against the same record types the service serialises.

The five-project layout picks the third option and structures the
rest of the code around it. Once you've split out `.Interfaces`, the
remaining boundaries (`Models`, `Core`, `Web`, `Sdk`) follow
naturally.

## Canonical layout

| Suffix         | Role                                                                              | Java analogue            |
|----------------|-----------------------------------------------------------------------------------|--------------------------|
| `.Interfaces`  | Public DTOs, request/response records, enums. No business logic, no I/O.          | `*-interfaces`           |
| `.Models`      | Persistence entities + repository contracts. Replace in-memory store with EF Core.| `*-models`               |
| `.Core`        | Commands, queries, handlers, mappers, business services. Holds the rules.         | `*-core`                 |
| `.Web`         | Runnable ASP.NET Core 10 host: `Program.cs`, controllers / minimal-API endpoints. | `*-web` (Spring Boot)    |
| `.Sdk`         | Typed `HttpClient` for other services to call this one in-process.                | `*-sdk` (codegen target) |

The dependency graph is strictly layered:

```
Interfaces ◄── Models ◄── Core ◄── Web
   ▲
   └────── Sdk
```

`Sdk` consumes only `Interfaces` so external callers never pull in
your persistence layer or business logic. The compiler enforces this
— there's no project reference from `Sdk` to `Models` or `Core`.

## What each tier owns

```
                  ┌────────────────┐
                  │  .Interfaces   │  ← what the service exposes on the wire
                  │  DTOs / enums  │
                  └────────┬───────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
              ▼            │            ▼
      ┌────────────┐       │     ┌────────────┐
      │  .Models   │       │     │   .Sdk     │
      │ entities   │       │     │ HttpClient │
      │ repos      │       │     │ for callers│
      └─────┬──────┘       │     └────────────┘
            │              │
            ▼              │
      ┌────────────┐       │
      │  .Core     │       │
      │ commands   │       │
      │ queries    │       │
      │ handlers   │       │
      │ mappers    │       │
      └─────┬──────┘       │
            │              │
            ▼              │
      ┌────────────┐       │
      │  .Web      │       │
      │ Program.cs │       │
      │ controllers│       │
      └────────────┘       │
                           │
              ─ ─ ─ ─ ─ ─ ─┘  via Sdk reference
```

A consumer service references `.Sdk` only. The Sdk references
`.Interfaces`. Nothing leaks across.

## Available samples

| Sample              | Demonstrates                                                                  |
|---------------------|-------------------------------------------------------------------------------|
| `OrdersService`     | CQRS (command + query + cache) · idempotency · OpenAPI · Sdk · in-memory repo |

More samples will follow — event-sourced aggregate, saga orchestration,
inbound webhook handler, IDP-secured API. The five-project shape
stays the same; only the `.Core` and `.Web` content vary.

## Running the orders sample

```bash
source .envrc
dotnet run --project samples/FireflyFramework.Samples.OrdersService.Web
```

Then `curl http://localhost:5000/api/v1/orders` to confirm the host
is up. The Web README walks through the full demo flow (place order
→ replay via idempotency → read with query cache).

## Going further

| To learn how to…                            | Read                                                                  |
|---------------------------------------------|-----------------------------------------------------------------------|
| Compose the projects into a runnable service | [`docs/SERVICE-SCAFFOLDING.md`](../docs/SERVICE-SCAFFOLDING.md)        |
| Migrate from a Java Firefly service          | [`docs/MIGRATION-GUIDE.md`](../docs/MIGRATION-GUIDE.md)                |
| See the full module list                     | [`docs/MODULES.md`](../docs/MODULES.md)                                |
| Understand the layering rules                | [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md)                      |

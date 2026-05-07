# FireflyFramework.Cli

`firefly` — the .NET scaffolding CLI. Mirrors the Go `fireflyframework-cli`
binary and pyfly's `firefly` CLI.

## Install

```bash
dotnet tool install --global FireflyFramework.Cli
```

## Commands

| Command | Description |
|---|---|
| `firefly new <name> --tier=core` | Scaffold a 5-module microservice (Interfaces / Models / Core / Web / Sdk) |
| `firefly handler command CreateOrder` | Generate a `CreateOrderHandler` skeleton |
| `firefly handler query GetOrder` | Generate a query handler |
| `firefly saga PaymentSaga --steps=4` | Emit a saga with 4 placeholder steps + compensations |
| `firefly migration add_users_table` | Drop a Flyway-style timestamped migration file |
| `firefly help` | Print this list at runtime |

The CLI uses `FireflyFramework.Shell` for routing and `Scriban` for templates,
so the same patterns work inside an interactive `firefly>` shell session.

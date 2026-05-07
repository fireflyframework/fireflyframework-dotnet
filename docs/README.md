# Firefly Framework Documentation

Long-form documentation for the .NET port of the Firefly Framework.
Each file targets a specific audience or task; this index is the
recommended starting point.

| Document | Audience | What it covers |
|---|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Anyone integrating with the framework | The four-tier layering (Foundational → Platform → Adapters → Starters), inter-project dependency direction, the process model the runtime assumes, and the framework's versioning policy. |
| [`MODULES.md`](MODULES.md) | Anyone picking which package(s) to install | One-line description of every `src/*` project paired with its Java original, organised by tier. The "what's in this package" reference. |
| [`SERVICE-SCAFFOLDING.md`](SERVICE-SCAFFOLDING.md) | Service authors building on the framework | The canonical five-project layout (`.Interfaces` / `.Models` / `.Core` / `.Web` / `.Sdk`), naming conventions, dependency graph, and a step-by-step bootstrap recipe modelled on the Orders sample. |
| [`CONFIGURATION.md`](CONFIGURATION.md) | Service authors + operators | Every `Firefly:*` configuration section with the real `*Options` type signatures, default values, and worked examples. |
| [`MIGRATION-GUIDE.md`](MIGRATION-GUIDE.md) | Teams porting an existing Java service | Mapping table from Reactor / Spring DI / Spring Web / Spring Data R2DBC / Spring Cloud / Resilience4j / Micrometer / Logback to their .NET 10 counterparts, with side-by-side code samples. |

## Reading order

If you're new to the framework, read in this order:

1. **[`ARCHITECTURE.md`](ARCHITECTURE.md)** — orient on the layering model and the dependency direction. Five minutes.
2. **[`SERVICE-SCAFFOLDING.md`](SERVICE-SCAFFOLDING.md)** — see what a real service looks like end-to-end before reading individual module docs. Ten minutes.
3. **[`MODULES.md`](MODULES.md)** — skim the catalogue; bookmark the modules you'll use.
4. **[`CONFIGURATION.md`](CONFIGURATION.md)** — keep open while wiring up your `appsettings.json`.

If you're porting an existing Java service, start with [`MIGRATION-GUIDE.md`](MIGRATION-GUIDE.md) instead.

## Per-project documentation

Each project under [`src/`](../src) ships its own `README.md`
describing its public surface, options class, and short usage examples.
The same is true of every project under [`samples/`](../samples).
[`src/README.md`](../src/README.md) indexes them by tier.

## Top-level project README

The repository root [`README.md`](../README.md) covers the
five-minute pitch, the architecture diagram, the canonical
service-shape, two quickstart paths (run the sample / build your
first service from scratch), and the release-publish workflow.

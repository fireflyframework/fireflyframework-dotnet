# Contributing

## Build prerequisites

- .NET 10 SDK (`brew install dotnet` on macOS, see <https://dotnet.microsoft.com/download/dotnet/10.0>)
- A POSIX shell — sourcing `.envrc` exports `DOTNET_ROOT` and adds the SDK to your `PATH`

```bash
source .envrc
dotnet --version  # 10.0.x
```

## Build & test

```bash
dotnet build FireflyFramework.sln
dotnet test  tests/FireflyFramework.Tests/FireflyFramework.Tests.csproj
```

## Repository layout

```
fireflyframework-dotnet/
├── Directory.Build.props        # parent properties (net10.0, language version, package metadata)
├── Directory.Packages.props     # central package management = .NET equivalent of fireflyframework-bom
├── Directory.Build.targets      # cross-project targets (test framework refs etc.)
├── FireflyFramework.sln         # 57 projects (52 src + tests + 5 sample modules)
├── global.json                  # pins .NET 10 SDK
├── .envrc                       # `source` it to expose Homebrew dotnet
├── docs/                        # AUDIT, ARCHITECTURE, SERVICE-SCAFFOLDING, etc.
├── src/                         # 52 framework projects, one per Java module / sub-module
├── tests/                       # xUnit suites (157 tests passing)
├── samples/                     # five-project reference microservice
└── .github/workflows/ci.yml     # GitHub Actions: build, test, pack
```

## Adding a project

1. `mkdir src/FireflyFramework.NewModule`
2. Add a `.csproj` referencing only the projects you need; pull NuGet packages by name (versions resolve from `Directory.Packages.props`)
3. `dotnet sln FireflyFramework.sln add src/FireflyFramework.NewModule/FireflyFramework.NewModule.csproj`
4. Add a `README.md` summarising the public surface
5. Add tests in `tests/FireflyFramework.Tests/` or in a dedicated test project

## Conventions

- `net10.0` only (LTS, C# 14); nullable reference types enabled; implicit usings on
- File-scoped namespaces; primary constructors where natural; records for DTOs
- One Java `org.fireflyframework.*` package family ↔ one `FireflyFramework.*` project (sub-modules become `FireflyFramework.Module.SubModule`)
- Configuration sections follow `Firefly:<Module>:…`
- Wiring exposed as `IServiceCollection.AddFirefly<Module>(IConfiguration)` extensions
- Adapters use the `Firefly<Module>.<Provider>` namespace and a single `[<Module>Adapter("type", …)]` attribute (see `[EcmAdapter]`)

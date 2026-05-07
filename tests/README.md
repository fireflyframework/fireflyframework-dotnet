# Firefly Framework — `tests/`

The repository ships a single test project, `FireflyFramework.Tests`,
that exercises every public surface in the framework — both the
generic infrastructure layer and every concrete adapter — against the
real protocol or SDK shape it speaks. Adapter tests use
[WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) for HTTP
adapters and [NSubstitute](https://nsubstitute.github.io) for
SDK-fronted ones.

## Running

```bash
# All tests
dotnet test FireflyFramework.sln

# A single category (xunit fully-qualified name filter)
dotnet test FireflyFramework.sln --filter "FullyQualifiedName~Eureka"

# With coverage
dotnet test FireflyFramework.sln --collect:"XPlat Code Coverage"
```

## Layout

```
tests/
└── FireflyFramework.Tests/
    ├── *.cs                              one test class per framework area
    ├── FireflyFramework.Tests.csproj
    └── README.md                         this file
```

The test project is **non-packable** (`<IsPackable>false</IsPackable>`)
and is excluded from `dotnet pack`. It targets `net10.0` and references
every framework project plus the SDK helpers it needs.

## Test categories

The project's individual test files are grouped into three buckets.

### Framework internals

Verify the cross-cutting machinery — kernel exceptions, validators,
template rendering, error middleware, idempotency, cache, observability
metrics, data filters, CQRS bus, EDA serializers, event store,
orchestration engines, rule engine, plugins, callbacks, webhooks,
back-office context, config-server wiring.

### Adapter shape

Every concrete adapter the framework ships has a test that pins the
real wire / SDK shape it produces:

| Area | Adapter coverage |
|------|------------------|
| **Identity** | `KeycloakIdpAdapter`, `CognitoIdpAdapter`, `AzureAdIdpAdapter`, `InternalDbIdpAdapter` |
| **ECM storage** | `S3DocumentContentAdapter`, `AzureBlobDocumentContentAdapter` |
| **ECM e-signature** | `DocuSignSignatureEnvelopeAdapter`, `AdobeSignSignatureEnvelopeAdapter`, `LogaltySignatureEnvelopeAdapter` |
| **Notifications** | `SendGridEmailProvider`, `TwilioSmsProvider`, `ResendEmailProvider`, `FcmPushProvider` |
| **Service discovery** | `StaticServiceDiscoveryClient`, `EurekaServiceDiscoveryClient`, `ConsulServiceDiscoveryClient`, `KubernetesServiceDiscoveryClient` |

### Client + orchestration extras

The bundled helpers — load balancers, OAuth2 token cache, request
deduplicator, performance metrics, chaos handler, health rollup,
recovery service, topology rendering, workflow query, search projection,
REST control plane, scheduler, GraphQL client.

## How to add a test

1. Create a new file under `tests/FireflyFramework.Tests/` whose name ends in `Tests.cs`.
2. Make the class `public sealed`; put one `[Fact]` per scenario, or `[Theory]` + `[InlineData]` for parametrised cases.
3. For HTTP-based adapters, stand up a `WireMockServer.Start()` in the constructor and dispose in `Dispose`. Pin the request shape with `Request.Create().UsingPost().WithPath(...).WithBody(b => ...)`.
4. For SDK-fronted adapters, mock the underlying SDK interface with NSubstitute (e.g. `Substitute.For<IAmazonS3>()`). Assert with `client.Received(1).Method(...)`.
5. Prefer `Assert.True(...)` / `Assert.Equal(...)` over `FluentAssertions` chains; the codebase already mixes both, but new tests lean xUnit-native for readability.

The fastest feedback loop is `dotnet test --filter "FullyQualifiedName~YourClassName"` — it runs only the matching cases without re-discovering the rest of the suite.

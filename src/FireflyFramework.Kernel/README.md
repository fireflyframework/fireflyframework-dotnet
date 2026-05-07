# FireflyFramework.Kernel

Foundational primitives shared by every other module. This is the only
project in the framework with no project-reference dependencies — every
other tier transitively references it.

Mirrors `org.fireflyframework:firefly-common`.

## Public surface

### Exceptions

| Type                                  | Default error code              | When to use                                      |
|---------------------------------------|---------------------------------|--------------------------------------------------|
| `FireflyException`                    | `FIREFLY_ERROR`                 | Root of every framework-thrown exception         |
| `FireflyInfrastructureException`      | `FIREFLY_INFRASTRUCTURE_ERROR`  | Database, cache, messaging, networking failures  |
| `FireflySecurityException`            | `FIREFLY_SECURITY_ERROR`        | Authentication or authorization failures         |

Each exception carries:

- `Message` — the standard `Exception.Message`.
- `ErrorCode` — a stable string identifier suitable for log filters and
  alerting rules; surfaced into RFC 7807 `application/problem+json`
  responses by `FireflyFramework.Web`.
- `Context` — an immutable `IReadOnlyDictionary<string, object?>` that
  callers can attach diagnostic data to without subclassing. Use
  `WithContext("key", value)` to produce a copy enriched with one
  additional entry.

```csharp
using FireflyFramework.Kernel.Exceptions;

throw new FireflyInfrastructureException(
    "primary database is unreachable",
    errorCode: "DB_UNREACHABLE",
    cause: dbException);
```

### Calendar version

`FireflyVersion.Current` exposes the calendar version string
(`"26.04.01"`). It is kept in lockstep with the Java release line in
`fireflyframework-parent/pom.xml`. Other projects can read it to log a
startup banner or to check compatibility.

```csharp
using FireflyFramework.Kernel;

logger.LogInformation("Firefly Framework {Version} starting", FireflyVersion.Current);
```

## Dependencies

None — this is the bottom of the dependency graph. The csproj declares
no `<ProjectReference>` and no `<PackageReference>`.

## Java mapping

| .NET                                | Java original                                                          |
|-------------------------------------|------------------------------------------------------------------------|
| `FireflyException`                  | `org.fireflyframework.kernel.exception.FireflyException`               |
| `FireflyInfrastructureException`    | `org.fireflyframework.kernel.exception.FireflyInfrastructureException` |
| `FireflySecurityException`          | `org.fireflyframework.kernel.exception.FireflySecurityException`       |
| `FireflyVersion.Current`            | `${revision}` property in `fireflyframework-parent/pom.xml`            |

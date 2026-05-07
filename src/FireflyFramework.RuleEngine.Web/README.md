# FireflyFramework.RuleEngine.Web

## Overview

`FireflyFramework.RuleEngine.Web` exposes the rule-engine evaluator
over HTTP. It ships an ASP.NET Core controller —
`RulesEvaluationController` — that delegates every request to
`IRulesEvaluationService` from `FireflyFramework.RuleEngine.Core`. The
controller is the only piece in this assembly; everything else lives
in `Core` so an in-process consumer can evaluate rules without the
HTTP layer.

Mirrors `org.fireflyframework:firefly-common-rule-engine-web`. The
endpoints, JSON shapes, and HTTP verbs match the Java line one-to-one
so a hybrid Java/.NET deployment can mix services freely.

## Why a separate module?

Two reasons keep the controller in its own assembly:

1. **In-process consumers don't need ASP.NET.** The Java line allows
   an application to evaluate rules synchronously inside a service
   without exposing an HTTP endpoint, by referencing `Core` directly.
   The .NET port preserves that affordance: reference `Core` from a
   handler, ignore `Web`.
2. **Custom hosting is cheaper.** A team that wants to mount the
   controller under a different base path, behind their own auth
   pipeline, or with a custom OpenAPI overlay, can do it without
   forking the rest of the stack.

## Mental model

```
   HTTP client (RuleEngine.Sdk, curl, your service)
                  │
                  │  POST /api/rules/evaluate/direct  (base-64 YAML)
                  │  POST /api/rules/evaluate/plain   (plain YAML)
                  │  POST /api/rules/evaluate/by-code (rule code)
                  ▼
       ┌──────────────────────────────────┐
       │  RulesEvaluationController       │   ← this assembly
       │   - delegates to                 │
       │     IRulesEvaluationService      │
       └──────────┬───────────────────────┘
                  │
                  ▼
       ┌──────────────────────────────────┐
       │  RuleEngine.Core                 │
       │   YamlDslParser                  │
       │   AstRulesEvaluationEngine       │
       └──────────────────────────────────┘
                  │
                  ▼
       ┌──────────────────────────────────┐
       │  AstRulesEvaluationResult →      │
       │  RulesEvaluationResponseDto      │
       └──────────────────────────────────┘
```

The controller is intentionally thin — there's no logic in it beyond
binding request bodies and forwarding to the service.

## Endpoints

| Method | Path                            | Body                                | Description                                                |
|--------|---------------------------------|-------------------------------------|------------------------------------------------------------|
| POST   | `/api/rules/evaluate/direct`    | `RulesEvaluationRequestDto`         | Evaluate a base-64-encoded YAML rule against an input map  |
| POST   | `/api/rules/evaluate/plain`     | `PlainYamlEvaluationRequestDto`     | Evaluate a plain-text YAML rule against an input map       |
| POST   | `/api/rules/evaluate/by-code`   | `RuleEvaluationByCodeRequestDto`    | Look the rule up by its stable `code` and evaluate         |

Every endpoint returns a `RulesEvaluationResponseDto`. Non-2xx
responses follow the Firefly RFC 7807 conventions documented in
`FireflyFramework.Web` — a validation error (e.g. malformed YAML)
yields HTTP 400 with a `ProblemDetail` body.

### Request shapes

```jsonc
// POST /api/rules/evaluate/direct
{
  "base64YamlContent": "cnVsZU5hbWU6IHZpcC1kaXNjb3VudA==",
  "inputData": { "amount": 1500, "isVip": true }
}

// POST /api/rules/evaluate/plain
{
  "yamlContent": "ruleName: vip-discount\nconditions:\n  - $amount > 500\nactions:\n  - $tier = \"gold\"\n",
  "inputData": { "amount": 1500 }
}

// POST /api/rules/evaluate/by-code
{
  "ruleCode": "vip-discount",
  "inputData": { "amount": 1500, "isVip": true }
}
```

### Response shape

```json
{
  "success": true,
  "output": { "discount": 0.15, "tier": "gold" },
  "executionTimeMs": 4,
  "ruleCode": "vip-discount",
  "auditId": "f93b5f9c-8ad1-4cb1-bf60-3b5b7a4dba02",
  "errorMessage": null
}
```

## Wiring

```csharp
using FireflyFramework.RuleEngine.Core;
using FireflyFramework.RuleEngine.Web.Controllers;

builder.Services.AddSingleton<IRulesEvaluationService, RulesEvaluationService>();
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(RulesEvaluationController).Assembly);
```

`AddApplicationPart` is the bit that exposes the controller — without
it, ASP.NET only scans the host assembly. After wiring, the routes
are available under `/api/rules/evaluate/*` automatically.

## Common patterns

### Mounting under a non-default prefix

The `[Route("api/rules/evaluate")]` attribute defines the base path.
To override it (e.g. for backwards compatibility with an older path),
subclass and re-attribute:

```csharp
[Route("v2/rules")]
public sealed class V2RulesEvaluationController : RulesEvaluationController
{
    public V2RulesEvaluationController(IRulesEvaluationService s) : base(s) { }
}
```

(Then drop the original from the application part scan, or expose
both for the migration window.)

### Wrapping with an authorization policy

```csharp
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("rule-evaluator", p => p.RequireAuthenticatedUser().RequireRole("rule-evaluator"));
});

builder.Services.Configure<MvcOptions>(o =>
{
    // Apply [Authorize] to the controller globally
    o.Conventions.Add(new AuthorizeControllerConvention<RulesEvaluationController>("rule-evaluator"));
});
```

### Adding a circuit-breaker around the service

`RuleEngine.Web` is a presentation layer; resilience belongs to the
service or the upstream caller. Wrap `IRulesEvaluationService` with
a Polly decorator if you want failure isolation:

```csharp
services.Decorate<IRulesEvaluationService, PollyRuleService>();   // Scrutor
```

## Pitfalls and gotchas

- **The base path is `/api/rules/evaluate`, not `/api/rules`.**
  CRUD endpoints (rules, constants, audit) live in separate
  controllers when you wire them — the evaluator controller is the
  only one this module ships.
- **Body size matters.** A complex rule body in `direct` /`plain` can
  be tens of KB. Configure ASP.NET's
  `RequestSizeLimit` if your gateway sets a smaller default.
- **No CSRF protection out of the box.** The endpoints are POST and
  expect to be called from a service mesh, not a browser; if a
  consumer is browser-based, layer your standard CSRF mitigation on
  top.
- **Errors are mapped to RFC 7807.** A YAML parse failure surfaces as
  HTTP 400 with `type` set to `https://docs.firefly.com/errors/yaml-parse`.
  Don't treat a 5xx as a parse error; it indicates a server problem.
- **Cancellation is honoured.** A long-running rule evaluation
  respects the request's `CancellationToken` — abandoned client
  connections free server resources promptly.

## Internals (for the curious)

- The controller has zero injected services beyond
  `IRulesEvaluationService`. By design — adding logging or metrics
  here would duplicate what the service already records via
  `IAuditTrailService`.
- The endpoints accept `[FromBody]` only. Query strings and headers
  are deliberately ignored to keep the API shape uniform between
  HTTP and direct in-process invocation.
- `AddApplicationPart` resolves the controller assembly once at
  startup; adding new controllers to this assembly later requires a
  rebuild and redeploy.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.RuleEngine.Core`       | `IRulesEvaluationService`           |
| `FireflyFramework.RuleEngine.Interfaces` | DTOs                                |
| `Microsoft.AspNetCore.App`               | `[ApiController]`, MVC binding      |

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `RulesEvaluationController`   | `RulesEvaluationController`         |

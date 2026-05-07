# FireflyFramework.Web

ASP.NET Core 10 middleware and types for **RFC 7807 problem-detail
responses**, **idempotent writes**, **PII masking** in logs and
responses, and a complete **typed business-exception hierarchy** with
27 specialised exceptions covering every standard HTTP status the
framework emits.

This is the project that makes "every Firefly service emits the same
error envelope, the same correlation header, the same idempotency
semantics" true. It depends only on `FireflyFramework.Kernel` and the
`Microsoft.AspNetCore.App` framework reference — nothing else.

Mirrors `org.fireflyframework:firefly-web` plus
`firefly-spring-utils` (the Spring-boot auto-configuration glue from
the Java side, which translates here to `AddFireflyWeb`).

---

## What's in the box

Three concerns rolled into one project because they share the
request-pipeline lifecycle and benefit from being wired together:

1. **Exception → response mapping** — `GlobalExceptionHandlerMiddleware`
   catches every exception thrown downstream, runs it through a chain
   of `IExceptionConverter`s, and writes a `application/problem+json`
   response that's identical in shape to what the Java framework
   produces.
2. **Idempotent writes** — `IdempotencyMiddleware` caches the response
   of write requests carrying an `X-Idempotency-Key` so a retry from
   a flaky network produces the same response without re-running the
   handler.
3. **PII masking** — `PiiMaskingService` removes well-known sensitive
   fields (`password`, `ssn`, `cardNumber`, …) from JSON payloads and
   strings before logs or response bodies are emitted.

`AddFireflyWeb(IConfiguration)` wires all three with a single call.
`UseFireflyWeb()` adds the two middlewares to the request pipeline in
the right order.

---

## Why a dedicated web project?

Every microservice on the platform must expose **the same** error
envelope, **the same** idempotency contract, **the same** trace-id /
correlation-id propagation, **the same** PII-masking policy. If each
service team rolled their own, the platform fragments — every service
becomes a snowflake and on-call engineers can't write a generic
runbook.

This project is the answer: one DI registration call, one middleware
attach call, and every cross-service contract is enforced in code.
The application layer of a service ends up free of error-handling
boilerplate; throw a typed exception and the response shape is
guaranteed.

---

## Mental model

```
        ┌─────────────────────────────────────────────────────────────┐
        │                     ASP.NET Core pipeline                   │
        │                                                             │
        │   ┌─────────────────────────────────────────────────┐       │
        │   │  GlobalExceptionHandlerMiddleware (outermost)   │       │
        │   │  • Catches downstream throws                    │       │
        │   │  • Runs them through ExceptionConverterRegistry │       │
        │   │  • Fills traceId, spanId, correlationId         │       │
        │   │  • Optionally masks PII                         │       │
        │   │  • Writes application/problem+json              │       │
        │   └────────────────────┬────────────────────────────┘       │
        │                        │                                    │
        │   ┌────────────────────▼────────────────────────────┐       │
        │   │  IdempotencyMiddleware                          │       │
        │   │  • Reads X-Idempotency-Key                      │       │
        │   │  • If cache hit, replays cached response        │       │
        │   │  • If cache miss, runs the handler then caches  │       │
        │   └────────────────────┬────────────────────────────┘       │
        │                        │                                    │
        │                Endpoint / controller                        │
        └─────────────────────────────────────────────────────────────┘
```

The order matters: the exception handler is **outermost** so it sees
exceptions thrown from any inner middleware (including the
idempotency one). The idempotency middleware is downstream of
exception handling because we deliberately *don't* cache failed
responses — caching a 500 from a transient infra blip would replay
that failure to every retry.

---

## Quick start

```csharp
using FireflyFramework.Web.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFireflyWeb(builder.Configuration);

var app = builder.Build();
app.UseFireflyWeb();           // GlobalExceptionHandler + Idempotency
app.MapControllers();
app.Run();
```

Throw a typed exception from anywhere in your code and the response
is correctly shaped:

```csharp
[HttpPost("/api/withdrawals")]
public IActionResult Post(WithdrawalRequest body)
{
    if (body.Amount > _dailyLimit)
    {
        throw new BusinessException(
            "Withdrawal exceeds daily limit",
            errorCode: "WITHDRAWAL_LIMIT");
    }
    // …
}
```

The client receives:

```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/problem+json

{
  "timestamp":     "2026-04-01T12:34:56.789Z",
  "status":        422,
  "error":         "Unprocessable Entity",
  "message":       "Withdrawal exceeds daily limit",
  "code":          "WITHDRAWAL_LIMIT",
  "path":          "/api/withdrawals",
  "traceId":       "0af7651916cd43dd8448eb211c80319c",
  "spanId":        "b9c7c989f97918e1",
  "correlationId": "user-correlation-1234",
  "category":      "Business",
  "severity":      "Medium",
  "retryable":     false,
  "instance":      "/api/withdrawals"
}
```

---

## Wiring details

### `AddFireflyWeb(IConfiguration)`

The DI registration extension does six things:

1. **Binds four configuration sections** under `Firefly:Web:*`:
   `ErrorHandling`, `Idempotency`, `PiiMasking`, `Cors`. Each binds
   to a strongly-typed options class.
2. **Registers eight default `IExceptionConverter` implementations**
   covering common framework exceptions: `OperationCanceledException`,
   `JsonException`, `HttpRequestException`, `ArgumentException`,
   `UnauthorizedAccessException`, `InvalidOperationException`,
   `NotImplementedException`, `TimeoutException`. Service authors
   register additional converters by implementing
   `IExceptionConverter` and adding it to DI.
3. **Registers `PiiMaskingService` and `ExceptionConverterRegistry`
   as singletons** — both are stateless and safe to share.
4. **Adds an `IDistributedCache`** if the consumer hasn't already.
   Defaults to in-memory; production deployments typically replace
   this with a Redis cache via the `Firefly:Cache` configuration in
   `FireflyFramework.Cache`.
5. **Adds CORS** with the configured allowed origins and credentials
   policy.
6. **Adds a problem-details writer** that the middleware uses for
   serialisation.

### `UseFireflyWeb()`

Adds, in order:

1. `GlobalExceptionHandlerMiddleware`
2. `IdempotencyMiddleware`

Both are conditional on their `Enabled` flag in configuration —
disabling one in `appsettings.json` removes it from the pipeline
without code changes.

---

## Public surface

### Errors

| Type | Purpose |
|---|---|
| `ErrorResponse` | Full enterprise body shape: `timestamp`, `status`, `code`, `message`, `traceId`, `spanId`, `correlationId`, `category`, `severity`, `retryable`, `retryAfter`, validation errors, rate-limit info, circuit-breaker info, optional stack trace, optional debug info. |
| `ProblemDetail` | Strict RFC 7807 representation with extension members. `ProblemDetail.FromErrorResponse(...)` produces one from an `ErrorResponse`. |
| `IExceptionConverter` | SPI for translating an arbitrary exception into a partial `ErrorResponse`. Eight default implementations cover common framework exceptions; consumers register their own. |
| `ExceptionConverterRegistry` | Resolves the right converter for a thrown exception by walking the registered chain. The first match wins. |
| `ErrorCategory` | Enum: `Validation`, `Business`, `Authorization`, `Authentication`, `Resource`, `Concurrency`, `Infrastructure`, `External`, `Resilience`, `Cancelled`, `Unknown`. |
| `ValidationError` | Per-field error: `Field`, `Code`, `Message`, optional `RejectedValue`. Surfaced when `ValidationException` carries multiple field errors. |
| `RateLimitInfo` | `Limit`, `Remaining`, `ResetAt` carried in the response body for 429 responses (also written to `Retry-After`, `X-RateLimit-*` headers). |
| `CircuitBreakerInfo` | `BreakerName`, `OpenSinceUtc`, `Reason` carried for 503 responses caused by an open circuit breaker. |

### Typed exceptions (27)

Each carries an HTTP status, a stable `ErrorCode`, and the inherited
`Context` dictionary from `FireflyException`.

| Status | Exceptions |
|---|---|
| 400 | `ValidationException`, `InvalidRequestException` |
| 401 | `UnauthorizedException` |
| 403 | `ForbiddenException`, `AuthorizationException` |
| 404 | `ResourceNotFoundException` |
| 409 | `ConflictException`, `ConcurrencyException`, `DataIntegrityException` |
| 410 | `GoneException` |
| 412 | `PreconditionFailedException` |
| 413 | `PayloadTooLargeException` |
| 415 | `UnsupportedMediaTypeException` |
| 422 | `BusinessException` |
| 423 | `LockedResourceException` |
| 429 | `RateLimitException`, `QuotaExceededException` |
| 500 | `RetryExhaustedException` |
| 501 | `NotImplementedException` |
| 502 | `BadGatewayException`, `ThirdPartyServiceException` |
| 503 | `ServiceUnavailableException`, `CircuitBreakerException`, `BulkheadException`, `DegradedServiceException` |
| 504 | `OperationTimeoutException`, `GatewayTimeoutException` |

All inherit transitively from `FireflyException`, so a single
catch-all in middleware handles every framework-thrown error.

### Middleware

| Middleware | Purpose |
|---|---|
| `GlobalExceptionHandlerMiddleware` | Catches anything thrown downstream, runs it through `ExceptionConverterRegistry`, fills in trace IDs / correlation IDs / category / severity, optionally masks PII, writes `application/problem+json`. |
| `IdempotencyMiddleware` | Caches the response of write requests carrying `X-Idempotency-Key`. Configurable header / TTL / HTTP methods. `[DisableIdempotency]` opts out per action. |

### Helpers

* `PiiMaskingService` — masks sensitive JSON fields and string patterns before logging or serialisation.
* `[DisableIdempotency]` — endpoint-level attribute that opts out of idempotency caching for a specific endpoint.
* `FireflyBanner` — emits the ASCII banner at startup with the application name, version, and runtime.

---

## Configuration

```json
{
  "Firefly": {
    "Web": {
      "ErrorHandling": {
        "IncludeStackTrace":  false,
        "IncludeDebugInfo":   false,
        "ProblemTypeBaseUri": "https://errors.fireflyframework.org/",
        "MaskPii":            true
      },
      "Idempotency": {
        "Enabled":      true,
        "HeaderName":   "X-Idempotency-Key",
        "Ttl":          "24:00:00",
        "MaxKeyLength": 256,
        "Methods":      [ "POST", "PATCH", "PUT", "DELETE" ]
      },
      "PiiMasking": {
        "Enabled":         true,
        "MaskCharacter":   "*",
        "VisiblePrefix":   2,
        "VisibleSuffix":   2,
        "SensitiveFields": [ "password", "secret", "token", "apiKey", "authorization",
                             "ssn", "creditCard", "cardNumber", "cvv", "iban", "pin" ]
      },
      "Cors": {
        "AllowedOrigins":   [ "https://app.example.com" ],
        "AllowCredentials": false
      }
    }
  }
}
```

| Section | Effect |
|---|---|
| `ErrorHandling.IncludeStackTrace` | When true, the response carries `stackTrace` (do not enable in production). |
| `ErrorHandling.IncludeDebugInfo` | When true, the response carries the inner-exception chain. |
| `ErrorHandling.ProblemTypeBaseUri` | Base URI for the RFC 7807 `type` field; the error code is appended. |
| `ErrorHandling.MaskPii` | When true, `PiiMaskingService` runs over the response body and the inner-exception messages. |
| `Idempotency.Enabled` | Removes `IdempotencyMiddleware` from the pipeline when false. |
| `Idempotency.HeaderName` | The request header that carries the idempotency key. Default: `X-Idempotency-Key`. |
| `Idempotency.Ttl` | How long to keep a cached response. Default: 24 hours. |
| `Idempotency.MaxKeyLength` | Reject keys longer than this with a 400. Default: 256. |
| `Idempotency.Methods` | Which HTTP methods are eligible for idempotency caching. Default: `POST, PATCH, PUT, DELETE`. |
| `PiiMasking.Enabled` | Master switch. |
| `PiiMasking.MaskCharacter` | Character used for masked positions. Default: `*`. |
| `PiiMasking.VisiblePrefix` / `VisibleSuffix` | How many leading / trailing chars stay visible (so credit-card masks read `41**...****1234`). |
| `PiiMasking.SensitiveFields` | Property names (case-insensitive) to mask in JSON responses. |
| `Cors.AllowedOrigins` | List of origins permitted by the CORS policy. |
| `Cors.AllowCredentials` | If true, the CORS policy allows credentialed requests (cookies, Authorization). |

---

## Common patterns

### Throwing a business exception with structured context

```csharp
throw new BusinessException(
    message: "Withdrawal exceeds daily limit",
    errorCode: "WITHDRAWAL_LIMIT",
    context: new Dictionary<string, object?>
    {
        ["attemptedAmount"] = body.Amount,
        ["dailyLimit"]      = _dailyLimit,
        ["accountId"]       = accountId,
    });
```

The `context` dictionary surfaces into the RFC 7807 response as
extension members, so the client sees machine-readable data alongside
the human-readable message. PII masking runs over it just as it
would over any other JSON.

### Surfacing a multi-field validation error

```csharp
var errors = new List<ValidationError>
{
    new("debtorIban", "INVALID_IBAN", "Debtor IBAN failed mod-97 check"),
    new("currency",   "UNKNOWN_CURRENCY", "Currency 'ZZZ' is not in ISO 4217"),
};

throw new ValidationException(errors);
```

The middleware emits `400 Bad Request` with a `validationErrors` array
in the response body — clients render this directly into a form's
inline error UI.

### Custom exception converter

Register a converter to map a third-party SDK exception (here,
Stripe's `StripeException`) into the framework's response shape:

```csharp
public sealed class StripeExceptionConverter : IExceptionConverter
{
    public bool CanHandle(Exception ex) => ex is Stripe.StripeException;

    public ErrorResponse Convert(Exception ex, HttpContext ctx)
    {
        var stripe = (Stripe.StripeException)ex;
        return new ErrorResponse
        {
            Status     = (int)stripe.HttpStatusCode,
            Code       = $"STRIPE_{stripe.StripeError?.Code ?? "UNKNOWN"}",
            Message    = stripe.Message,
            Category   = ErrorCategory.External,
            Severity   = "High",
            Retryable  = stripe.HttpStatusCode is HttpStatusCode.ServiceUnavailable,
        };
    }
}

builder.Services.AddSingleton<IExceptionConverter, StripeExceptionConverter>();
```

The registry walks converters in registration order; the *first* one
to return `true` from `CanHandle` wins. Built-in converters run last.

### Opting an endpoint out of idempotency

```csharp
[HttpPost("/api/v1/orders/sweep")]
[DisableIdempotency]                // even with X-Idempotency-Key, never cache
public async Task<IActionResult> Sweep() { … }
```

Useful for "synthetic" write endpoints that should always re-run,
e.g. health probes that exercise the database, or admin clean-up jobs
that idempotency would surprise the caller about.

### Trace + correlation propagation

The middleware reads the standard W3C `traceparent` and the framework
`X-Correlation-Id` header from the inbound request, attaches them to
the `Activity.Current`, and writes them back into the response as
`traceId`, `spanId`, `correlationId`. Downstream services receive them
on outbound calls when they use `FireflyFramework.Client.Rest.RestClientBuilder`.

---

## Pitfalls and gotchas

**Don't `await` past the response stream.** `UseFireflyWeb` writes the
response inside the middleware. If a downstream handler writes to the
response *before* throwing, the framework can't replace the body.
Always throw before any `Response.WriteAsync(...)`.

**Don't include `IncludeStackTrace = true` in production.** The stack
trace is rendered into the response body and leaks implementation
detail. Toggle it on locally for debugging only.

**Don't re-throw `OperationCanceledException`.** The framework's
default converter maps it to `499 Client Closed Request` (a deliberate
non-standard status for "client gave up"). Re-throwing as a
`BusinessException` would lose that semantic and tell on-call alerts
that real failures happened when the client just disconnected.

**Don't put the same `X-Idempotency-Key` on logically-different
requests.** The cache key is just `(method, path, key)`, not the
request body. Two different `POST /orders` with `body=A` and `body=B`
under the same key will return the response of whichever ran first.
Use a fresh GUID per logical operation.

**`DisableIdempotency` doesn't return an error if the client sends
the header.** It simply ignores it and runs the handler. Some teams
prefer to reject the request loudly — to do that, write a small custom
filter that checks for the header.

**The PII masker is a fixed allow-list, not a deep semantic scanner.**
Add field names through configuration; don't rely on it to find
unstructured PII embedded in free-text. For free-text PII, use a
dedicated tokeniser before logging.

---

## Internals (for the curious)

`GlobalExceptionHandlerMiddleware` does its work in the catch block of
a try/await/exception sandwich. It deliberately uses `Response.Body`
directly rather than `Response.WriteAsJsonAsync` because it needs to
serialise *after* setting `StatusCode` and `Headers["Content-Type"]`,
which the higher-level helpers don't let you order correctly.

The exception converter chain is iterated in O(n) per exception. We
profiled a converter cache keyed by exception type but the cache
hit-rate in production was high enough that the overhead of the
ConcurrentDictionary lookup wasn't worth the complexity. Eight
converters is fast.

The idempotency cache key is computed as
`SHA-256(method | path | idempotencyKey)`, then hex-encoded. We don't
include the body because (a) computing a hash over the body would
require fully buffering it, and (b) the spec for `Idempotency-Key`
deliberately says the *caller* commits to "same key implies same
intent" — the server isn't supposed to second-guess.

`PiiMaskingService` walks JSON via `Utf8JsonReader` and rewrites with
`Utf8JsonWriter`, both stack-allocated. The masker doesn't materialise
a `JsonDocument`, which keeps allocations bounded under hot load. The
field-name match is case-insensitive but ordinal, not culture-aware,
so a Turkish `i`/`I` doesn't trip up the matcher.

The 27 typed exceptions exist because the alternative — a single
`HttpException(int status, string message)` — would lose the type
discrimination that makes `catch (UnauthorizedException)` an
expressive pattern. The compiler complains earlier, the on-call log
filters become trivial, and the stack-trace symbolicates to a
human-readable name.

---

## Dependencies

| Reference | Used for |
|---|---|
| `FireflyFramework.Kernel` (project) | Base `FireflyException`, error code, context dictionary |
| `Microsoft.AspNetCore.App` (FrameworkRef) | Middleware abstractions, `HttpContext`, `IDistributedCache`, `System.Text.Json` |

That's it. The Web project doesn't depend on any third-party NuGet —
the entire feature set is built on the BCL plus ASP.NET Core itself.

---

## Java mapping

| .NET | Java |
|---|---|
| `GlobalExceptionHandlerMiddleware` | `GlobalExceptionHandler` + `ExceptionHandlerAutoConfiguration` |
| `IdempotencyMiddleware` | `IdempotencyAutoConfiguration` + `IdempotencyCache` |
| `PiiMaskingService` | `PiiMaskingService` + `PiiMaskingProperties` |
| `ExceptionConverterRegistry` | `ExceptionConverterService` |
| `IExceptionConverter` | `ExceptionConverter` interface |
| `[DisableIdempotency]` | `@DisableIdempotency` |
| `BusinessException`, `ValidationException`, … | `BusinessException`, `ValidationException`, … (same names, same codes) |
| `ProblemDetail` | `ProblemDetail` (Spring's RFC 7807 representation) |
| `ErrorResponse` | `ErrorResponse` |

The wire shape is identical — a service running version *X* on the
Java line emits a response that a service running version *X* on the
.NET line could have emitted. This is the cross-runtime
compatibility commitment the framework makes.

---

## See also

* [`FireflyFramework.Kernel`](../FireflyFramework.Kernel/README.md) — the base `FireflyException` and stable error codes.
* [`FireflyFramework.Cache`](../FireflyFramework.Cache/README.md) — the distributed cache that backs idempotency in production.
* [`FireflyFramework.Observability`](../FireflyFramework.Observability/README.md) — trace and correlation IDs propagated by the middleware.
* [`docs/CONFIGURATION.md`](../../docs/CONFIGURATION.md) — every `Firefly:Web:*` configuration section.

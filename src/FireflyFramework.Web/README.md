# FireflyFramework.Web

ASP.NET Core 9 middleware and types for RFC 7807 problem-detail
responses, idempotent writes, PII masking, and a complete typed
business-exception hierarchy. Mirrors `org.fireflyframework:firefly-web`
plus `firefly-spring-utils`.

## Wiring

```csharp
using FireflyFramework.Web.DependencyInjection;

builder.Services.AddFireflyWeb(builder.Configuration);

var app = builder.Build();
app.UseFireflyWeb();   // GlobalExceptionHandlerMiddleware + IdempotencyMiddleware
```

`AddFireflyWeb`:

- binds `Firefly:Web:ErrorHandling`, `Firefly:Web:Idempotency`,
  `Firefly:Web:PiiMasking`, and `Firefly:Web:Cors` configuration sections;
- registers eight default `IExceptionConverter` implementations
  (timeout, JSON parse, HTTP, argument, unauthorised, invalid operation,
  not implemented, operation cancelled);
- registers `PiiMaskingService` and `ExceptionConverterRegistry` as
  singletons;
- adds `IDistributedCache` (in-memory) so the idempotency middleware
  has a backing store out of the box.

`UseFireflyWeb` adds the two middlewares to the request pipeline.

## Public surface

### Errors

- `ErrorResponse` — full enterprise body shape: timestamp, status,
  code, message, traceId, spanId, correlationId, category, severity,
  retryable, retryAfter, validation errors, rate-limit info,
  circuit-breaker info, optional stack trace, optional debug info.
- `ProblemDetail` — strict RFC 7807 representation with extension
  members. `ProblemDetail.FromErrorResponse(...)` converts.
- `IExceptionConverter` SPI plus eight default implementations. Add
  your own by registering them in DI.

### Typed exceptions

Twenty-seven types covering every standard HTTP status used by the
framework. Each carries an HTTP status, a stable `ErrorCode`, and the
inherited `Context` dictionary.

| Status | Exceptions                                                                              |
|--------|-----------------------------------------------------------------------------------------|
| 400    | `ValidationException`, `InvalidRequestException`                                        |
| 401    | `UnauthorizedException`                                                                 |
| 403    | `ForbiddenException`, `AuthorizationException`                                          |
| 404    | `ResourceNotFoundException`                                                             |
| 409    | `ConflictException`, `ConcurrencyException`, `DataIntegrityException`                   |
| 410    | `GoneException`                                                                         |
| 412    | `PreconditionFailedException`                                                           |
| 413    | `PayloadTooLargeException`                                                              |
| 415    | `UnsupportedMediaTypeException`                                                         |
| 422    | `BusinessException`                                                                     |
| 423    | `LockedResourceException`                                                               |
| 429    | `RateLimitException`, `QuotaExceededException`                                          |
| 500    | `RetryExhaustedException`                                                               |
| 501    | `NotImplementedException`                                                               |
| 502    | `BadGatewayException`, `ThirdPartyServiceException`                                     |
| 503    | `ServiceUnavailableException`, `CircuitBreakerException`, `BulkheadException`, `DegradedServiceException` |
| 504    | `OperationTimeoutException`, `GatewayTimeoutException`                                  |

### Middleware

| Middleware                          | Purpose                                                                                |
|-------------------------------------|----------------------------------------------------------------------------------------|
| `GlobalExceptionHandlerMiddleware`  | Catches anything thrown downstream, runs it through `ExceptionConverterRegistry`, fills in trace IDs / correlation IDs / category / severity, optionally masks PII, writes `application/problem+json` |
| `IdempotencyMiddleware`             | Caches the response of write requests carrying `X-Idempotency-Key` (configurable header / TTL / methods); `[DisableIdempotency]` opts out per action  |

### Helpers

- `PiiMaskingService` — masks sensitive JSON fields and string patterns
  before logging or serialisation.
- `[DisableIdempotency]` — endpoint-level attribute that opts out of
  idempotency caching.

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

## Example response body

A `BusinessException` thrown from the application is converted to:

```json
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
  "instance":      "/api/withdrawals?account=123"
}
```

## Dependencies

| Reference                                  | Used for                                  |
|--------------------------------------------|-------------------------------------------|
| `FireflyFramework.Kernel`                  | Base `FireflyException`                   |
| `Microsoft.AspNetCore.App` (FrameworkRef)  | Middleware, `HttpContext`                 |
| `System.Text.Json`                         | Problem-details serialisation             |

## Java mapping

| .NET                                | Java                                                                  |
|-------------------------------------|-----------------------------------------------------------------------|
| `GlobalExceptionHandlerMiddleware`  | `GlobalExceptionHandler` + `ExceptionHandlerAutoConfiguration`        |
| `IdempotencyMiddleware`             | `IdempotencyAutoConfiguration` + `IdempotencyCache`                   |
| `PiiMaskingService`                 | `PiiMaskingService` + `PiiMaskingProperties`                          |
| `ExceptionConverterRegistry`        | `ExceptionConverterService`                                           |
| `[DisableIdempotency]`              | `@DisableIdempotency`                                                 |

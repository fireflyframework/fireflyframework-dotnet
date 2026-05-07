# FireflyFramework.Web

ASP.NET Core middleware and types for RFC 7807 problem-detail responses, idempotent writes, PII masking and a complete business-exception hierarchy. Mirrors `fireflyframework-web`.

## Quick start

```csharp
builder.Services.AddFireflyWeb(builder.Configuration);
// ...
app.UseFireflyWeb(); // adds GlobalExceptionHandlerMiddleware + IdempotencyMiddleware
```

## What's inside

### Errors

- **`ErrorResponse`** — enterprise body shape: timestamp, status, code, message, traceId, spanId, correlationId, category, severity, retryable, retryAfter, validation errors, rate-limit info, circuit-breaker info, optional stack trace, optional debug info.
- **`ProblemDetail`** — strict RFC 7807 representation. `ProblemDetail.FromErrorResponse(...)` converts.
- **27 business exceptions** — `BusinessException` (422), `ValidationException` (400), `InvalidRequestException` (400), `UnauthorizedException` (401), `ForbiddenException`/`AuthorizationException` (403), `ResourceNotFoundException` (404), `ConflictException`/`ConcurrencyException`/`DataIntegrityException` (409), `GoneException` (410), `PreconditionFailedException` (412), `PayloadTooLargeException` (413), `UnsupportedMediaTypeException` (415), `LockedResourceException` (423), `RateLimitException`/`QuotaExceededException` (429), `RetryExhaustedException` (500), `NotImplementedException` (501), `BadGatewayException`/`ThirdPartyServiceException` (502), `ServiceUnavailableException`/`CircuitBreakerException`/`BulkheadException`/`DegradedServiceException` (503), `OperationTimeoutException`/`GatewayTimeoutException` (504).
- **`IExceptionConverter`** SPI + 8 default converters (timeout, JSON parse, HTTP, argument, etc.). Register additional ones in DI to extend.

### Middleware

- **`GlobalExceptionHandlerMiddleware`** — catches anything thrown downstream, runs it through `ExceptionConverterRegistry`, fills in trace IDs / correlation IDs / category / severity, optionally masks PII, writes `application/problem+json`. Mirrors Java `GlobalExceptionHandler`.
- **`IdempotencyMiddleware`** — caches the response of write requests carrying `X-Idempotency-Key` (configurable header / TTL / methods). `[DisableIdempotency]` opts out per action.
- **`PiiMaskingService`** — masks sensitive JSON fields and string patterns before logging or serialization.

## Configuration

```jsonc
{
  "Firefly": {
    "Web": {
      "ErrorHandling": {
        "IncludeStackTrace": false,
        "IncludeDebugInfo": false,
        "ProblemTypeBaseUri": "https://errors.fireflyframework.org/",
        "MaskPii": true
      },
      "Idempotency": {
        "Enabled": true,
        "HeaderName": "X-Idempotency-Key",
        "Ttl": "24:00:00",
        "MaxKeyLength": 256,
        "Methods": [ "POST", "PATCH", "PUT", "DELETE" ]
      },
      "PiiMasking": {
        "Enabled": true,
        "MaskCharacter": "*",
        "VisiblePrefix": 2,
        "VisibleSuffix": 2,
        "SensitiveFields": [ "password", "secret", "token", "apiKey", "authorization", "ssn", "creditCard", "cardNumber", "cvv", "iban", "pin" ]
      },
      "Cors": {
        "AllowedOrigins": [ "https://app.example.com" ],
        "AllowCredentials": false
      }
    }
  }
}
```

## Example response body

```json
{
  "timestamp": "2026-04-01T12:34:56.789Z",
  "status": 422,
  "error": "Unprocessable Entity",
  "message": "Withdrawal exceeds daily limit",
  "code": "WITHDRAWAL_LIMIT",
  "path": "/api/withdrawals",
  "traceId": "0af7651916cd43dd8448eb211c80319c",
  "spanId": "b9c7c989f97918e1",
  "correlationId": "user-correlation-1234",
  "category": "Business",
  "severity": "Medium",
  "retryable": false,
  "instance": "/api/withdrawals?account=123"
}
```

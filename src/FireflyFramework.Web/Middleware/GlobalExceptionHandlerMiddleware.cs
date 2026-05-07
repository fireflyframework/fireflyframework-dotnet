// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;
using FireflyFramework.Kernel.Exceptions;
using FireflyFramework.Web.Errors.Converters;
using FireflyFramework.Web.Errors.Exceptions;
using FireflyFramework.Web.Errors.Models;
using FireflyFramework.Web.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Web.Middleware;

/// <summary>
/// Centralised exception handler middleware. Translates raw exceptions through the
/// <see cref="ExceptionConverterRegistry"/>, fills in trace + correlation IDs, applies PII
/// masking, and writes a single <c>application/problem+json</c> response. Mirrors Java
/// <c>GlobalExceptionHandler</c>.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly RequestDelegate _next;
    private readonly ExceptionConverterRegistry _converters;
    private readonly PiiMaskingService? _pii;
    private readonly GlobalExceptionHandlerOptions _options;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _log;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ExceptionConverterRegistry converters,
        IOptions<GlobalExceptionHandlerOptions> options,
        ILogger<GlobalExceptionHandlerMiddleware> log,
        PiiMaskingService? pii = null)
    {
        _next = next;
        _converters = converters;
        _options = options.Value;
        _log = log;
        _pii = pii;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var translated = ex switch
            {
                ServiceException se => se,
                FireflyException fe => new ServiceException(fe.Message, 500, fe.ErrorCode, ErrorCategory.Technical, ErrorSeverity.High, false, null, fe),
                _ => _converters.TryConvert(ex, context)
                     ?? new ServiceException(ex.Message, 500, "INTERNAL_ERROR", ErrorCategory.Technical, ErrorSeverity.High, false, null, ex),
            };

            await WriteResponseAsync(context, translated).ConfigureAwait(false);
        }
    }

    private async Task WriteResponseAsync(HttpContext context, ServiceException ex)
    {
        if (context.Response.HasStarted)
        {
            _log.LogWarning(ex, "Response already started; cannot translate exception");
            return;
        }

        var activity = Activity.Current;
        var response = new ErrorResponse
        {
            Status = ex.HttpStatus,
            Error = ReasonPhrases.GetReasonPhrase(ex.HttpStatus),
            Message = MaskIfNeeded(ex.Message),
            Code = ex.ErrorCode,
            Path = context.Request.Path,
            Category = ex.Category,
            Severity = ex.Severity,
            Retryable = ex.Retryable,
            RetryAfter = ex.RetryAfter,
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            CorrelationId = context.Request.Headers["X-Correlation-Id"].ToString(),
            RequestId = context.TraceIdentifier,
            Instance = $"{context.Request.Path}{context.Request.QueryString}",
        };

        switch (ex)
        {
            case ValidationException vex:
                response.Errors = vex.Errors.ToList();
                break;
            case RateLimitException rex:
                response.RateLimitInfo = rex.Info;
                if (rex.RetryAfter is not null)
                {
                    context.Response.Headers["Retry-After"] = rex.RetryAfter.ToString();
                }
                break;
            case CircuitBreakerException cbe:
                response.CircuitBreakerInfo = cbe.Info;
                break;
        }

        if (_options.IncludeStackTrace)
        {
            response.StackTrace = ex.ToString();
        }

        if (_options.IncludeDebugInfo && ex.InnerException is not null)
        {
            response.DebugInfo = new Dictionary<string, object?>
            {
                ["innerType"] = ex.InnerException.GetType().FullName,
                ["innerMessage"] = ex.InnerException.Message,
            };
        }

        context.Response.StatusCode = ex.HttpStatus;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
    }

    private string MaskIfNeeded(string message) =>
        _options.MaskPii && _pii is not null ? _pii.MaskString(message) : message;
}

internal static class ReasonPhrases
{
    public static string GetReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        409 => "Conflict",
        410 => "Gone",
        412 => "Precondition Failed",
        413 => "Payload Too Large",
        415 => "Unsupported Media Type",
        422 => "Unprocessable Entity",
        423 => "Locked",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        501 => "Not Implemented",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => "Error",
    };
}

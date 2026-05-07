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

using System.Text.Json;
using FireflyFramework.Web.Errors.Converters;
using FireflyFramework.Web.Errors.Exceptions;
using FireflyFramework.Web.Logging;
using FireflyFramework.Web.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

public class WebMiddlewareTests
{
    private static GlobalExceptionHandlerMiddleware Build(RequestDelegate next, GlobalExceptionHandlerOptions? opt = null)
    {
        var converters = new ExceptionConverterRegistry(new IExceptionConverter[]
        {
            new OperationCanceledExceptionConverter(),
            new TimeoutExceptionConverter(),
            new JsonExceptionConverter(),
            new ArgumentExceptionConverter(),
        });

        return new GlobalExceptionHandlerMiddleware(
            next,
            converters,
            Options.Create(opt ?? new GlobalExceptionHandlerOptions()),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance);
    }

    [Fact]
    public async Task Middleware_translates_BusinessException_to_422_problem_json()
    {
        var ctx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var middleware = Build(_ => throw new BusinessException("not allowed", "WIDGET_REJECTED"));
        await middleware.InvokeAsync(ctx);
        ctx.Response.StatusCode.Should().Be(422);
        ctx.Response.ContentType.Should().Contain("problem+json");
        ctx.Response.Body.Position = 0;
        var doc = await JsonDocument.ParseAsync(ctx.Response.Body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("WIDGET_REJECTED");
    }

    [Fact]
    public async Task Middleware_translates_TimeoutException_to_504()
    {
        var ctx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var middleware = Build(_ => throw new TimeoutException("Took too long"));
        await middleware.InvokeAsync(ctx);
        ctx.Response.StatusCode.Should().Be(504);
    }

    [Fact]
    public async Task Middleware_includes_validation_errors_in_response()
    {
        var ctx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var failures = new[] { new Web.Errors.Models.ValidationError("amount", "MIN", "must be > 0") };
        var middleware = Build(_ => throw new ValidationException("bad payload", failures));
        await middleware.InvokeAsync(ctx);
        ctx.Response.StatusCode.Should().Be(400);
        ctx.Response.Body.Position = 0;
        var doc = await JsonDocument.ParseAsync(ctx.Response.Body);
        doc.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("amount");
    }
}

public class PiiMaskingTests
{
    [Fact]
    public void Masks_default_sensitive_fields_in_json()
    {
        var svc = new PiiMaskingService(Options.Create(new PiiMaskingOptions()));
        var json = JsonDocument.Parse("""{"username":"alice","password":"S3cretPass!","email":"a@b.com"}""");
        var masked = svc.MaskJson(json.RootElement);
        masked.GetProperty("username").GetString().Should().Be("alice");
        masked.GetProperty("password").GetString().Should().NotContain("S3cret");
        masked.GetProperty("email").GetString().Should().Be("a@b.com");
    }

    [Fact]
    public void Mask_value_keeps_visible_prefix_and_suffix()
    {
        var svc = new PiiMaskingService(Options.Create(new PiiMaskingOptions { VisiblePrefix = 2, VisibleSuffix = 2 }));
        svc.MaskValue("4111111111111111").Should().Be("41************11");
    }
}

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

using System.Net.Http;
using System.Text.Json;
using FireflyFramework.Web.Errors.Exceptions;
using Microsoft.AspNetCore.Http;

namespace FireflyFramework.Web.Errors.Converters;

/// <summary>Translates <see cref="OperationCanceledException"/> into a 504-style timeout.</summary>
public sealed class OperationCanceledExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(OperationCanceledException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new OperationTimeoutException("Operation was cancelled or timed out", exception);
}

/// <summary>Translates <see cref="TimeoutException"/> into a 504.</summary>
public sealed class TimeoutExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(TimeoutException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new OperationTimeoutException(exception.Message, exception);
}

/// <summary>Translates JSON parsing errors into a validation error.</summary>
public sealed class JsonExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(JsonException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new ValidationException("Malformed JSON payload", null, exception);
}

/// <summary>Translates upstream HTTP failures into a 502.</summary>
public sealed class HttpRequestExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(HttpRequestException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new ThirdPartyServiceException(exception.Message, exception);
}

/// <summary>Translates <see cref="UnauthorizedAccessException"/>.</summary>
public sealed class UnauthorizedAccessExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(UnauthorizedAccessException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new ForbiddenException(exception.Message, exception);
}

/// <summary>Translates <see cref="ArgumentException"/> and subclasses to 400.</summary>
public sealed class ArgumentExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(ArgumentException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new InvalidRequestException(exception.Message, exception);
}

/// <summary>Translates <see cref="System.NotImplementedException"/>.</summary>
public sealed class NotImplementedExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(System.NotImplementedException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new Exceptions.NotImplementedException(exception.Message, exception);
}

/// <summary>Translates <see cref="InvalidOperationException"/> to 409.</summary>
public sealed class InvalidOperationExceptionConverter : IExceptionConverter
{
    public Type ExceptionType => typeof(InvalidOperationException);

    public ServiceException Convert(Exception exception, HttpContext context) =>
        new ConflictException(exception.Message, exception);
}

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

using System;
using System.Collections.Generic;

namespace FireflyFramework.Kernel.Exceptions;

/// <summary>
/// Root exception for every Firefly Framework error. Mirrors
/// <c>org.fireflyframework.kernel.exception.FireflyException</c>: carries a stable
/// <see cref="ErrorCode"/> and an open <see cref="Context"/> dictionary so callers can
/// attach diagnostic data without subclassing.
/// </summary>
public class FireflyException : Exception
{
    private const string DefaultErrorCode = "FIREFLY_ERROR";

    public string ErrorCode { get; }

    public IReadOnlyDictionary<string, object?> Context { get; }

    public FireflyException()
        : this(string.Empty, DefaultErrorCode, null, null) { }

    public FireflyException(string message)
        : this(message, DefaultErrorCode, null, null) { }

    public FireflyException(string message, string errorCode)
        : this(message, errorCode, null, null) { }

    public FireflyException(string message, Exception? cause)
        : this(message, DefaultErrorCode, null, cause) { }

    public FireflyException(string message, string errorCode, Exception? cause)
        : this(message, errorCode, null, cause) { }

    public FireflyException(
        string message,
        string errorCode,
        IDictionary<string, object?>? context,
        Exception? cause)
        : base(message, cause)
    {
        ErrorCode = string.IsNullOrEmpty(errorCode) ? DefaultErrorCode : errorCode;
        Context = context is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(context);
    }

    /// <summary>
    /// Returns a copy of this exception with one extra context entry. Use when an
    /// outer layer wants to enrich an exception without losing the original cause.
    /// </summary>
    public FireflyException WithContext(string key, object? value)
    {
        var next = new Dictionary<string, object?>(Context) { [key] = value };
        return new FireflyException(Message, ErrorCode, next, InnerException);
    }
}

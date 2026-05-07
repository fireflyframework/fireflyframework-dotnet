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

namespace FireflyFramework.Web.Middleware;

/// <summary>Configuration for <see cref="GlobalExceptionHandlerMiddleware"/>. Mirrors Java <c>ErrorHandlingProperties</c>.</summary>
public sealed class GlobalExceptionHandlerOptions
{
    public const string SectionName = "Firefly:Web:ErrorHandling";

    /// <summary>Include exception stack traces in the response body. Should be off in production.</summary>
    public bool IncludeStackTrace { get; set; }

    /// <summary>Include the inner-exception chain in the <c>debugInfo</c> map. Off by default.</summary>
    public bool IncludeDebugInfo { get; set; }

    /// <summary>RFC 7807 type-URI prefix used when generating <c>type</c> URIs.</summary>
    public string ProblemTypeBaseUri { get; set; } = "https://errors.fireflyframework.org/";

    /// <summary>Apply PII masking to the error <c>message</c> and <c>details</c> fields.</summary>
    public bool MaskPii { get; set; } = true;
}

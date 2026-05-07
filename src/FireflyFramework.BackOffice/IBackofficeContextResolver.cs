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

using Microsoft.AspNetCore.Http;

namespace FireflyFramework.BackOffice;

/// <summary>
/// Resolves a <see cref="BackofficeContext"/> from the inbound HTTP request. Mirrors Java
/// <c>BackofficeContextResolver</c>. Default expectations:
/// <list type="bullet">
///   <item><c>X-User-Id</c> — back-office user UUID (required)</item>
///   <item><c>X-Impersonate-Party-Id</c> — impersonated customer UUID (required)</item>
///   <item><c>X-Tenant-Id</c> — optional tenant UUID</item>
///   <item><c>X-Impersonation-Reason</c> — optional reason string</item>
/// </list>
/// </summary>
public interface IBackofficeContextResolver
{
    Task<BackofficeContext> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);

    Task<BackofficeContext> ResolveAsync(
        HttpContext httpContext,
        Guid? contractId,
        Guid? productId,
        CancellationToken ct = default);

    Task<bool> ValidateImpersonationAsync(
        Guid backofficeUserId,
        Guid impersonatedPartyId,
        HttpContext httpContext,
        CancellationToken ct = default);

    int Priority => 0;
    bool Supports(HttpContext httpContext) => true;
}

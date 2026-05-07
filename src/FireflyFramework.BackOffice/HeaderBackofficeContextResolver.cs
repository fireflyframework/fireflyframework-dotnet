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
/// Default resolver that extracts UUIDs from Istio-injected headers. Override
/// <see cref="ValidateImpersonationAsync"/> to integrate with your security center.
/// </summary>
public class HeaderBackofficeContextResolver : IBackofficeContextResolver
{
    public const string UserIdHeader = "X-User-Id";
    public const string ImpersonatePartyHeader = "X-Impersonate-Party-Id";
    public const string TenantHeader = "X-Tenant-Id";
    public const string ImpersonationReasonHeader = "X-Impersonation-Reason";

    public Task<BackofficeContext> ResolveAsync(HttpContext httpContext, CancellationToken ct = default) =>
        ResolveAsync(httpContext, contractId: null, productId: null, ct);

    public virtual Task<BackofficeContext> ResolveAsync(
        HttpContext httpContext,
        Guid? contractId,
        Guid? productId,
        CancellationToken ct = default)
    {
        var userId = ParseGuid(httpContext, UserIdHeader)
            ?? throw new InvalidOperationException($"Missing required header {UserIdHeader}");
        var partyId = ParseGuid(httpContext, ImpersonatePartyHeader)
            ?? throw new InvalidOperationException($"Missing required header {ImpersonatePartyHeader}");

        var ctx = new BackofficeContext(
            BackofficeUserId: userId,
            ImpersonatedPartyId: partyId,
            ContractId: contractId,
            ProductId: productId,
            TenantId: ParseGuid(httpContext, TenantHeader),
            ImpersonationStartedAt: DateTimeOffset.UtcNow,
            ImpersonationReason: httpContext.Request.Headers[ImpersonationReasonHeader].FirstOrDefault(),
            BackofficeUserIpAddress: httpContext.Connection.RemoteIpAddress?.ToString());

        return Task.FromResult(ctx);
    }

    /// <summary>Default returns true; override to call your security center / permission service.</summary>
    public virtual Task<bool> ValidateImpersonationAsync(
        Guid backofficeUserId,
        Guid impersonatedPartyId,
        HttpContext httpContext,
        CancellationToken ct = default) => Task.FromResult(true);

    private static Guid? ParseGuid(HttpContext ctx, string header) =>
        Guid.TryParse(ctx.Request.Headers[header].FirstOrDefault(), out var g) ? g : null;
}

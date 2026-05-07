// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Security.Claims;

namespace FireflyFramework.Security.Core;

/// <summary>
/// Authenticated principal and granted authorities for a request. Mirrors
/// Spring <c>SecurityContext</c> / pyfly <c>SecurityContext</c>.
/// </summary>
public sealed record SecurityContext(
    string SubjectId,
    string? Username,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Authorities,
    string? TenantId,
    IReadOnlyDictionary<string, string> Claims)
{
    public static readonly SecurityContext Anonymous = new(
        SubjectId: string.Empty,
        Username: null,
        Roles: Array.Empty<string>(),
        Authorities: Array.Empty<string>(),
        TenantId: null,
        Claims: new Dictionary<string, string>());

    public bool IsAuthenticated => !string.IsNullOrEmpty(SubjectId);
    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool HasAuthority(string authority) => Authorities.Contains(authority, StringComparer.OrdinalIgnoreCase);
    public bool HasAnyRole(params string[] roles) => roles.Any(HasRole);

    public static SecurityContext FromClaimsPrincipal(ClaimsPrincipal principal, string? tenantId = null)
    {
        if (principal.Identity?.IsAuthenticated != true) return Anonymous;
        var sub = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var username = principal.FindFirstValue("preferred_username") ?? principal.Identity.Name;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var authorities = principal.FindAll("authority").Select(c => c.Value).ToArray();
        var claims = principal.Claims.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.First().Value);
        return new SecurityContext(sub, username, roles, authorities, tenantId, claims);
    }
}

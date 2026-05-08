// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Security.Configuration;

/// <summary>
/// Configuration root for the security module. Mirrors Spring Security
/// properties: a JWT bearer block, a password-encoder block, and an
/// optional list of path-based access rules.
/// </summary>
public sealed class FireflySecurityOptions
{
    public const string SectionName = "Firefly:Security";

    /// <summary>JWT bearer authentication settings.</summary>
    public JwtOptions Jwt { get; set; } = new();

    /// <summary>Password encoder selection used by <c>IPasswordEncoder</c>.</summary>
    public PasswordOptions Password { get; set; } = new();

    /// <summary>Optional declarative access rules (advisory — used by route-level filters).</summary>
    public List<AccessRule> Rules { get; set; } = new();
}

/// <summary>JWT bearer settings: issuer, audience, signing key, lifetimes, optional Authority for OIDC.</summary>
public sealed class JwtOptions
{
    /// <summary>Token <c>iss</c> claim. Tokens with a different issuer are rejected.</summary>
    public string Issuer { get; set; } = "firefly";

    /// <summary>Token <c>aud</c> claim. Tokens with a different audience are rejected.</summary>
    public string Audience { get; set; } = "firefly";

    /// <summary>HMAC-SHA256 signing secret. Use a 32+ byte high-entropy string.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Default lifetime for access tokens issued by <c>HmacJwtTokenService</c>.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Default lifetime for refresh tokens (advisory; the framework does not issue refresh tokens directly).</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>OIDC discovery URL when delegating signing-key resolution to a remote authority.</summary>
    public string? Authority { get; set; }

    /// <summary>If <c>true</c>, the OIDC discovery endpoint must be HTTPS. Set to <c>false</c> only in dev.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}

/// <summary>Password encoder configuration.</summary>
public sealed class PasswordOptions
{
    /// <summary>Encoder algorithm: <c>BCrypt</c> (default) or <c>Noop</c>.</summary>
    public string Encoder { get; set; } = "BCrypt";

    /// <summary>BCrypt cost parameter (10–12 production; 4 for tests).</summary>
    public int BCryptWorkFactor { get; set; } = 12;
}

/// <summary>Path-based access rule (advisory; filters typically prefer <c>[PreAuthorize]</c>).</summary>
public sealed class AccessRule
{
    /// <summary>Path pattern (Ant-style: <c>**</c> matches any segments).</summary>
    public string Path { get; set; } = "/**";

    /// <summary>Rule type: <c>PermitAll</c>, <c>Authenticated</c>, <c>HasRole</c>, <c>HasAnyRole</c>, or <c>DenyAll</c>.</summary>
    public string Type { get; set; } = "PermitAll";

    /// <summary>Roles required by <c>HasRole</c> / <c>HasAnyRole</c> rule types.</summary>
    public List<string> Roles { get; set; } = new();
}

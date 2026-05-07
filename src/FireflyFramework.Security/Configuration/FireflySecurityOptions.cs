// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Security.Configuration;

/// <summary>Configuration root for the security module. Mirrors Spring Security properties.</summary>
public sealed class FireflySecurityOptions
{
    public const string SectionName = "Firefly:Security";

    public JwtOptions Jwt { get; set; } = new();
    public PasswordOptions Password { get; set; } = new();
    public List<AccessRule> Rules { get; set; } = new();
}

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "firefly";
    public string Audience { get; set; } = "firefly";
    public string Secret { get; set; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
    public string? Authority { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
}

public sealed class PasswordOptions
{
    public string Encoder { get; set; } = "BCrypt";
    public int BCryptWorkFactor { get; set; } = 12;
}

public sealed class AccessRule
{
    public string Path { get; set; } = "/**";
    public string Type { get; set; } = "PermitAll"; // PermitAll | Authenticated | HasRole | HasAnyRole | DenyAll
    public List<string> Roles { get; set; } = new();
}

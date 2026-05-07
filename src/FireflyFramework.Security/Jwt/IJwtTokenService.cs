// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FireflyFramework.Security.Jwt;

/// <summary>
/// Issues and validates JWT bearer tokens. Mirrors pyfly <c>JWTService</c>.
/// </summary>
public interface IJwtTokenService
{
    string Issue(IDictionary<string, object> claims, TimeSpan? lifetime = null);
    ClaimsPrincipal Validate(string token);
}

public sealed class HmacJwtTokenService : IJwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;
    private readonly TimeSpan _defaultLifetime;
    private readonly string _algorithm;

    public HmacJwtTokenService(string issuer, string audience, string secret, TimeSpan? defaultLifetime = null)
    {
        _issuer = issuer;
        _audience = audience;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _defaultLifetime = defaultLifetime ?? TimeSpan.FromHours(1);
        _algorithm = SecurityAlgorithms.HmacSha256;
    }

    public string Issue(IDictionary<string, object> claims, TimeSpan? lifetime = null)
    {
        var creds = new SigningCredentials(_key, _algorithm);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims.Select(kv => new Claim(kv.Key, kv.Value?.ToString() ?? "")),
            notBefore: now,
            expires: now.Add(lifetime ?? _defaultLifetime),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var p = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _issuer,
            ValidAudience = _audience,
            IssuerSigningKey = _key,
            ClockSkew = TimeSpan.FromSeconds(30),
        }, out _);
        return p;
    }
}

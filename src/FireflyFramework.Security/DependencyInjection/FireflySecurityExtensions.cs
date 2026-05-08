// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Text;
using FireflyFramework.Security.Authorization;
using FireflyFramework.Security.Configuration;
using FireflyFramework.Security.Core;
using FireflyFramework.Security.Crypto;
using FireflyFramework.Security.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FireflyFramework.Security.DependencyInjection;

public static class FireflySecurityExtensions
{
    /// <summary>
    /// Registers the Firefly security primitives (<see cref="ISecurityContextHolder"/>,
    /// <see cref="IAuthorizationEvaluator"/>, <see cref="IPasswordEncoder"/>,
    /// <see cref="IJwtTokenService"/>) and wires JWT bearer authentication / authorization
    /// against <c>Firefly:Security</c>.
    /// </summary>
    public static IServiceCollection AddFireflySecurity(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflySecurityOptions>().Bind(config.GetSection(FireflySecurityOptions.SectionName));

        services.TryAddSingleton<ISecurityContextHolder, AsyncLocalSecurityContextHolder>();
        services.TryAddSingleton<IAuthorizationEvaluator, DefaultAuthorizationEvaluator>();

        services.TryAddSingleton<IPasswordEncoder>(sp =>
        {
            var p = sp.GetRequiredService<IOptions<FireflySecurityOptions>>().Value.Password;
            return p.Encoder.Equals("Noop", StringComparison.OrdinalIgnoreCase)
                ? new NoopPasswordEncoder()
                : new BCryptPasswordEncoder(p.BCryptWorkFactor);
        });

        services.TryAddSingleton<IJwtTokenService>(sp =>
        {
            var jwt = sp.GetRequiredService<IOptions<FireflySecurityOptions>>().Value.Jwt;
            return new HmacJwtTokenService(jwt.Issuer, jwt.Audience, jwt.Secret, jwt.AccessTokenLifetime);
        });

        // Register the bearer scheme without resolving options yet — JwtBearerOptions itself
        // is bound later via IConfigureNamedOptions, which means we never have to call
        // services.BuildServiceProvider() inside an AddJwtBearer callback (a known anti-pattern
        // that creates a duplicate root container and breaks scoped resolution).
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureFireflyJwtBearer>();

        services.AddAuthorization();
        return services;
    }

    private sealed class ConfigureFireflyJwtBearer : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly IOptions<FireflySecurityOptions> _options;

        public ConfigureFireflyJwtBearer(IOptions<FireflySecurityOptions> options) { _options = options; }

        public void Configure(JwtBearerOptions opts) => Configure(JwtBearerDefaults.AuthenticationScheme, opts);

        public void Configure(string? name, JwtBearerOptions opts)
        {
            if (name is not null && name != JwtBearerDefaults.AuthenticationScheme) return;
            var jwt = _options.Value.Jwt;
            opts.Authority = jwt.Authority;
            opts.RequireHttpsMetadata = jwt.RequireHttpsMetadata;
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = !string.IsNullOrEmpty(jwt.Secret),
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = string.IsNullOrEmpty(jwt.Secret) ? null : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                NameClaimType = "preferred_username",
                RoleClaimType = "roles",
            };
        }
    }
}

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

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                var sp = services.BuildServiceProvider();
                var jwt = sp.GetRequiredService<IOptions<FireflySecurityOptions>>().Value.Jwt;
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
            });

        services.AddAuthorization();
        return services;
    }
}

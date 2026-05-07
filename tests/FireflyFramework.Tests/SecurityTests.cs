// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Security.Claims;
using FireflyFramework.Security.Authorization;
using FireflyFramework.Security.Core;
using FireflyFramework.Security.Crypto;
using FireflyFramework.Security.Jwt;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void SecurityContext_FromClaimsPrincipal_extracts_roles_and_username()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "user-1"),
            new Claim("preferred_username", "alice"),
            new Claim(ClaimTypes.Role, "ADMIN"),
            new Claim(ClaimTypes.Role, "USER"),
        }, authenticationType: "test");

        var ctx = SecurityContext.FromClaimsPrincipal(new ClaimsPrincipal(identity), tenantId: "tenant-99");

        ctx.IsAuthenticated.Should().BeTrue();
        ctx.SubjectId.Should().Be("user-1");
        ctx.Username.Should().Be("alice");
        ctx.Roles.Should().BeEquivalentTo(new[] { "ADMIN", "USER" });
        ctx.TenantId.Should().Be("tenant-99");
        ctx.HasAnyRole("ADMIN", "MANAGER").Should().BeTrue();
    }

    [Fact]
    public async Task DefaultEvaluator_handles_compound_expressions()
    {
        var evaluator = new DefaultAuthorizationEvaluator();
        var ctx = new SecurityContext("u", "u", new[] { "ADMIN" }, new[] { "ORDERS_WRITE" }, null, new Dictionary<string, string>());

        (await evaluator.EvaluateAsync("hasRole('ADMIN') and hasAuthority('ORDERS_WRITE')", ctx)).Should().BeTrue();
        (await evaluator.EvaluateAsync("hasRole('ADMIN') and hasAuthority('NOT_GRANTED')", ctx)).Should().BeFalse();
        (await evaluator.EvaluateAsync("hasRole('USER') or hasRole('ADMIN')", ctx)).Should().BeTrue();
        (await evaluator.EvaluateAsync("!hasRole('USER')", ctx)).Should().BeTrue();
        (await evaluator.EvaluateAsync("isAuthenticated()", ctx)).Should().BeTrue();
        (await evaluator.EvaluateAsync("isAuthenticated() and !hasRole('GUEST')", ctx)).Should().BeTrue();
    }

    [Fact]
    public void BCryptPasswordEncoder_round_trips_password()
    {
        var encoder = new BCryptPasswordEncoder(workFactor: 4);
        var encoded = encoder.Encode("hunter2");

        encoder.Matches("hunter2", encoded).Should().BeTrue();
        encoder.Matches("wrong", encoded).Should().BeFalse();
        encoded.Should().NotContain("hunter2");
    }

    [Fact]
    public void Jwt_round_trips_with_validation()
    {
        var svc = new HmacJwtTokenService("issuer", "audience", "this-is-a-very-long-secret-key-for-hmac-256");
        var token = svc.Issue(new Dictionary<string, object>
        {
            ["sub"] = "u-1",
            ["preferred_username"] = "alice",
        }, lifetime: TimeSpan.FromMinutes(5));

        var principal = svc.Validate(token);
        principal.FindFirstValue("sub").Should().Be("u-1");
        principal.FindFirstValue("preferred_username").Should().Be("alice");
    }

    [Fact]
    public void AsyncLocalSecurityContextHolder_pushes_and_pops()
    {
        var holder = new AsyncLocalSecurityContextHolder();
        holder.Current.IsAuthenticated.Should().BeFalse();
        var ctx = new SecurityContext("u", "u", new[] { "X" }, Array.Empty<string>(), null, new Dictionary<string, string>());
        using (holder.Push(ctx))
        {
            holder.Current.SubjectId.Should().Be("u");
        }
        holder.Current.SubjectId.Should().BeEmpty();
    }
}

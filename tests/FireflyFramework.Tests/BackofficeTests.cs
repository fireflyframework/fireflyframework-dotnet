using FireflyFramework.BackOffice;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FireflyFramework.Tests;

public class BackofficeTests
{
    private static HttpContext NewContext(IDictionary<string, string>? headers = null)
    {
        var http = new DefaultHttpContext();
        if (headers is not null)
        {
            foreach (var kv in headers) http.Request.Headers[kv.Key] = kv.Value;
        }
        return http;
    }

    [Fact]
    public void BackofficeContext_role_helpers_handle_null_collections()
    {
        var ctx = new BackofficeContext(Guid.NewGuid(), Guid.NewGuid());
        ctx.HasBackofficeRole("admin").Should().BeFalse();
        ctx.HasAnyBackofficeRole("admin", "support").Should().BeFalse();
        ctx.HasAllBackofficeRoles("admin").Should().BeFalse();
        ctx.IsValidImpersonation().Should().BeTrue();
    }

    [Fact]
    public void BackofficeContext_role_helpers_match_supplied_sets()
    {
        var ctx = new BackofficeContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            BackofficeRoles: new HashSet<string> { "admin", "support" },
            BackofficePermissions: new HashSet<string> { "customers:read" });

        ctx.HasBackofficeRole("admin").Should().BeTrue();
        ctx.HasAnyBackofficeRole("auditor", "admin").Should().BeTrue();
        ctx.HasAllBackofficeRoles("admin", "support").Should().BeTrue();
        ctx.HasAllBackofficeRoles("admin", "auditor").Should().BeFalse();
        ctx.HasBackofficePermission("customers:read").Should().BeTrue();
    }

    [Fact]
    public async Task HeaderResolver_throws_without_required_headers()
    {
        var resolver = new HeaderBackofficeContextResolver();
        await FluentActions.Invoking(() => resolver.ResolveAsync(NewContext()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HeaderResolver_extracts_uuid_headers()
    {
        var user = Guid.NewGuid();
        var party = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        var ctx = NewContext(new Dictionary<string, string>
        {
            [HeaderBackofficeContextResolver.UserIdHeader] = user.ToString(),
            [HeaderBackofficeContextResolver.ImpersonatePartyHeader] = party.ToString(),
            [HeaderBackofficeContextResolver.TenantHeader] = tenant.ToString(),
            [HeaderBackofficeContextResolver.ImpersonationReasonHeader] = "support-ticket-123",
        });

        var resolver = new HeaderBackofficeContextResolver();
        var result = await resolver.ResolveAsync(ctx);

        result.BackofficeUserId.Should().Be(user);
        result.ImpersonatedPartyId.Should().Be(party);
        result.TenantId.Should().Be(tenant);
        result.ImpersonationReason.Should().Be("support-ticket-123");
        result.IsValidImpersonation().Should().BeTrue();
    }

    [Fact]
    public async Task HeaderResolver_passes_through_contract_and_product()
    {
        var contract = Guid.NewGuid();
        var product = Guid.NewGuid();
        var ctx = NewContext(new Dictionary<string, string>
        {
            [HeaderBackofficeContextResolver.UserIdHeader] = Guid.NewGuid().ToString(),
            [HeaderBackofficeContextResolver.ImpersonatePartyHeader] = Guid.NewGuid().ToString(),
        });

        var resolver = new HeaderBackofficeContextResolver();
        var result = await resolver.ResolveAsync(ctx, contract, product);

        result.ContractId.Should().Be(contract);
        result.ProductId.Should().Be(product);
        result.HasContract().Should().BeTrue();
        result.HasProduct().Should().BeTrue();
    }

    [Fact]
    public void SecurityContext_required_role_checks()
    {
        var sec = new BackofficeSecurityContext(
            Endpoint: "/admin/users/{id}",
            HttpMethod: "GET",
            RequiredBackofficeRoles: new HashSet<string> { "admin", "auditor" });

        sec.HasRequiredBackofficeRoles().Should().BeTrue();
        sec.RequiresBackofficeRole("admin").Should().BeTrue();
        sec.RequiresBackofficeRole("intern").Should().BeFalse();
    }
}

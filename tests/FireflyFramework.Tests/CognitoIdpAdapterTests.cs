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

using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using FireflyFramework.Idp;
using FireflyFramework.Idp.AwsCognito;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// NSubstitute-mocked tests for <see cref="CognitoIdpAdapter"/>. Verify each method
/// dispatches the correct AWS request and reads the correct fields from the response,
/// without ever touching real AWS.
/// </summary>
public sealed class CognitoIdpAdapterTests
{
    private readonly IAmazonCognitoIdentityProvider _client;
    private readonly CognitoIdpAdapter _adapter;

    public CognitoIdpAdapterTests()
    {
        _client = Substitute.For<IAmazonCognitoIdentityProvider>();
        var options = Options.Create(new CognitoOptions
        {
            Region = "eu-west-1",
            UserPoolId = "eu-west-1_test",
            ClientId = "client-1",
            ClientSecret = "secret-1",
        });
        _adapter = new CognitoIdpAdapter(options, _client);
    }

    [Fact]
    public async Task LoginAsync_UsesUserPasswordAuth_ReturnsToken()
    {
        _client.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "acc",
                    RefreshToken = "ref",
                    IdToken = "idt",
                    TokenType = "Bearer",
                    ExpiresIn = 3600,
                },
            });

        var resp = await _adapter.LoginAsync(new LoginRequest("alice", "pwd"), CancellationToken.None);

        Assert.Equal("acc", resp.AccessToken);
        Assert.Equal("ref", resp.RefreshToken);
        Assert.Equal("idt", resp.IdToken);
        Assert.Equal(3600, resp.ExpiresIn);

        await _client.Received(1).InitiateAuthAsync(
            Arg.Is<InitiateAuthRequest>(r =>
                r.AuthFlow == AuthFlowType.USER_PASSWORD_AUTH &&
                r.ClientId == "client-1" &&
                r.AuthParameters.ContainsKey("USERNAME") &&
                r.AuthParameters.ContainsKey("PASSWORD") &&
                r.AuthParameters.ContainsKey("SECRET_HASH")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_UsesRefreshTokenAuth()
    {
        _client.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "newAcc",
                    IdToken = "newIdt",
                    TokenType = "Bearer",
                    ExpiresIn = 900,
                },
            });

        var resp = await _adapter.RefreshAsync(new RefreshRequest("oldRef"), CancellationToken.None);

        Assert.Equal("newAcc", resp.AccessToken);
        Assert.Equal("oldRef", resp.RefreshToken);   // Falls back to the supplied token if response omits one.

        await _client.Received(1).InitiateAuthAsync(
            Arg.Is<InitiateAuthRequest>(r =>
                r.AuthFlow == AuthFlowType.REFRESH_TOKEN_AUTH &&
                r.AuthParameters["REFRESH_TOKEN"] == "oldRef"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IntrospectAsync_GetUserSucceeds_ReturnsActive()
    {
        _client.GetUserAsync(Arg.Any<GetUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetUserResponse
            {
                Username = "alice",
                UserAttributes = new List<AttributeType>
                {
                    new() { Name = "email", Value = "a@x.com" },
                },
            });

        var resp = await _adapter.IntrospectAsync("acc", CancellationToken.None);

        Assert.True(resp.Active);
        Assert.Equal("alice", resp.Username);
    }

    [Fact]
    public async Task IntrospectAsync_NotAuthorized_ReturnsInactive()
    {
        _client.GetUserAsync(Arg.Any<GetUserRequest>(), Arg.Any<CancellationToken>())
            .Throws(new NotAuthorizedException("invalid"));

        var resp = await _adapter.IntrospectAsync("bad", CancellationToken.None);

        Assert.False(resp.Active);
    }

    [Fact]
    public async Task CreateUserAsync_SendsAdminCreateUserRequest()
    {
        _client.AdminCreateUserAsync(Arg.Any<AdminCreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminCreateUserResponse
            {
                User = new UserType { Username = "alice" },
            });

        var resp = await _adapter.CreateUserAsync(
            new CreateUserRequest(
                Username: "alice",
                Email: "a@x.com",
                Password: "P@ssw0rd!",
                GivenName: "Alice",
                FamilyName: "Smith",
                Roles: null,
                Attributes: null),
            CancellationToken.None);

        Assert.Equal("alice", resp.UserId);

        await _client.Received(1).AdminCreateUserAsync(
            Arg.Is<AdminCreateUserRequest>(r =>
                r.UserPoolId == "eu-west-1_test" &&
                r.Username == "alice" &&
                r.TemporaryPassword == "P@ssw0rd!"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserAsync_SendsAdminDeleteUserRequest()
    {
        _client.AdminDeleteUserAsync(Arg.Any<AdminDeleteUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminDeleteUserResponse());

        await _adapter.DeleteUserAsync("alice", CancellationToken.None);

        await _client.Received(1).AdminDeleteUserAsync(
            Arg.Is<AdminDeleteUserRequest>(r => r.UserPoolId == "eu-west-1_test" && r.Username == "alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRolesAsync_ListsGroups_ReturnsGroupNames()
    {
        _client.ListGroupsAsync(Arg.Any<ListGroupsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListGroupsResponse
            {
                Groups = new List<GroupType>
                {
                    new() { GroupName = "admins" },
                    new() { GroupName = "users" },
                },
            });

        var roles = await _adapter.GetRolesAsync(CancellationToken.None);

        Assert.Equal(2, roles.Count);
        Assert.Contains("admins", roles);
        Assert.Contains("users", roles);
    }

    [Fact]
    public async Task AssignRolesToUserAsync_AddsToEachGroup()
    {
        _client.AdminAddUserToGroupAsync(Arg.Any<AdminAddUserToGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminAddUserToGroupResponse());

        await _adapter.AssignRolesToUserAsync(
            new AssignRolesRequest("alice", new[] { "admins", "users" }),
            CancellationToken.None);

        await _client.Received(1).AdminAddUserToGroupAsync(
            Arg.Is<AdminAddUserToGroupRequest>(r => r.GroupName == "admins"),
            Arg.Any<CancellationToken>());
        await _client.Received(1).AdminAddUserToGroupAsync(
            Arg.Is<AdminAddUserToGroupRequest>(r => r.GroupName == "users"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeSessionAsync_CallsAdminUserGlobalSignOut()
    {
        _client.AdminUserGlobalSignOutAsync(Arg.Any<AdminUserGlobalSignOutRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminUserGlobalSignOutResponse());

        await _adapter.RevokeSessionAsync("alice", "ignored", CancellationToken.None);

        await _client.Received(1).AdminUserGlobalSignOutAsync(
            Arg.Is<AdminUserGlobalSignOutRequest>(r => r.Username == "alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MfaChallengeAsync_DocumentsUpstreamLimitation()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _adapter.MfaChallengeAsync("alice", CancellationToken.None));
    }

    [Fact]
    public async Task ListSessionsAsync_DocumentsUpstreamLimitation()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _adapter.ListSessionsAsync("alice", CancellationToken.None));
    }

    [Fact]
    public async Task CreateScopeAsync_DocumentsUpstreamLimitation()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _adapter.CreateScopeAsync(new CreateScopeRequest("s", null), CancellationToken.None));
    }

    [Fact]
    public void Constructor_RejectsNullClient()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CognitoIdpAdapter(Options.Create(new CognitoOptions { Region = "eu-west-1" }), client: null!));
    }
}

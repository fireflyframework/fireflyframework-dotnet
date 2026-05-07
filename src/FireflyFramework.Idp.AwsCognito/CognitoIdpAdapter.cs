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

using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using FireflyFramework.Idp;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Idp.AwsCognito;

/// <summary>AWS Cognito <see cref="IIdpAdapter"/> implementation. Mirrors Java <c>CognitoIdpAdapter</c>.</summary>
public sealed class CognitoIdpAdapter : IIdpAdapter
{
    private readonly IAmazonCognitoIdentityProvider _client;
    private readonly CognitoOptions _opt;

    /// <summary>
    /// Production constructor. Builds a default <see cref="AmazonCognitoIdentityProviderClient"/>
    /// for the configured region. Pick this overload when registering through DI.
    /// </summary>
    public CognitoIdpAdapter(IOptions<CognitoOptions> options)
        : this(options, new AmazonCognitoIdentityProviderClient(RegionEndpoint.GetBySystemName(options.Value.Region)))
    {
    }

    /// <summary>
    /// Constructor that accepts an explicit <see cref="IAmazonCognitoIdentityProvider"/>.
    /// Used by tests with a mocked client and by hosts that share a single Cognito client
    /// across adapters or wire custom credentials providers / retry policies.
    /// </summary>
    public CognitoIdpAdapter(IOptions<CognitoOptions> options, IAmazonCognitoIdentityProvider client)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        _opt = options.Value;
        _client = client;
    }

    private string? SecretHash(string username) => _opt.ClientSecret is null
        ? null
        : Convert.ToBase64String(new HMACSHA256(Encoding.UTF8.GetBytes(_opt.ClientSecret))
            .ComputeHash(Encoding.UTF8.GetBytes(username + _opt.ClientId)));

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var authParams = new Dictionary<string, string>
        {
            ["USERNAME"] = request.Username,
            ["PASSWORD"] = request.Password,
        };

        if (SecretHash(request.Username) is { } sh) authParams["SECRET_HASH"] = sh;

        var resp = await _client.InitiateAuthAsync(new InitiateAuthRequest
        {
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            ClientId = _opt.ClientId,
            AuthParameters = authParams,
        }, ct).ConfigureAwait(false);

        var r = resp.AuthenticationResult ?? throw new InvalidOperationException("Cognito returned no authentication result");
        return new TokenResponse(r.AccessToken, r.RefreshToken, r.TokenType ?? "Bearer", r.ExpiresIn, null, r.IdToken);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var resp = await _client.InitiateAuthAsync(new InitiateAuthRequest
        {
            AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
            ClientId = _opt.ClientId,
            AuthParameters = new Dictionary<string, string> { ["REFRESH_TOKEN"] = request.RefreshToken },
        }, ct).ConfigureAwait(false);

        var r = resp.AuthenticationResult!;
        return new TokenResponse(r.AccessToken, r.RefreshToken ?? request.RefreshToken, r.TokenType ?? "Bearer", r.ExpiresIn, null, r.IdToken);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        await _client.GlobalSignOutAsync(new GlobalSignOutRequest { AccessToken = request.RefreshToken }, ct).ConfigureAwait(false);
    }

    public async Task<IntrospectionResponse> IntrospectAsync(string accessToken, CancellationToken ct = default)
    {
        // Cognito has no token-introspection endpoint (it's an OAuth2 server, not OIDC for that),
        // so we call GetUser which the SDK validates against the access token. If it succeeds,
        // the token is active; otherwise the SDK throws.
        try
        {
            var resp = await _client.GetUserAsync(new GetUserRequest { AccessToken = accessToken }, ct).ConfigureAwait(false);
            var attrs = resp.UserAttributes?.ToDictionary(a => a.Name, a => (object?)a.Value) ?? new();
            return new IntrospectionResponse(true, resp.Username, resp.Username, null, attrs);
        }
        catch (NotAuthorizedException)
        {
            return new IntrospectionResponse(false, null, null, null, null);
        }
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        await _client.RevokeTokenAsync(new RevokeTokenRequest
        {
            ClientId = _opt.ClientId,
            ClientSecret = _opt.ClientSecret ?? string.Empty,
            Token = refreshToken,
        }, ct).ConfigureAwait(false);
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var resp = await _client.GetUserAsync(new GetUserRequest { AccessToken = accessToken }, ct).ConfigureAwait(false);
        var attrs = resp.UserAttributes?.ToDictionary(a => a.Name, a => (object?)a.Value) ?? new();
        return new UserInfoResponse(
            resp.Username,
            attrs.TryGetValue("email", out var e) ? e?.ToString() : null,
            attrs.TryGetValue("given_name", out var g) ? g?.ToString() : null,
            attrs.TryGetValue("family_name", out var f) ? f?.ToString() : null,
            null, attrs);
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var resp = await _client.AdminCreateUserAsync(new AdminCreateUserRequest
        {
            UserPoolId = _opt.UserPoolId,
            Username = request.Username,
            UserAttributes = new List<AttributeType>
            {
                new() { Name = "email", Value = request.Email },
                new() { Name = "given_name", Value = request.GivenName ?? string.Empty },
                new() { Name = "family_name", Value = request.FamilyName ?? string.Empty },
            },
            TemporaryPassword = request.Password,
        }, ct).ConfigureAwait(false);

        return new CreateUserResponse(resp.User?.Username ?? request.Username);
    }

    public async Task<UpdateUserResponse> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        var attrs = new List<AttributeType>();
        if (request.Email is not null) attrs.Add(new AttributeType { Name = "email", Value = request.Email });
        if (request.GivenName is not null) attrs.Add(new AttributeType { Name = "given_name", Value = request.GivenName });
        if (request.FamilyName is not null) attrs.Add(new AttributeType { Name = "family_name", Value = request.FamilyName });
        await _client.AdminUpdateUserAttributesAsync(new AdminUpdateUserAttributesRequest
        {
            UserPoolId = _opt.UserPoolId,
            Username = request.UserId,
            UserAttributes = attrs,
        }, ct).ConfigureAwait(false);
        return new UpdateUserResponse(request.UserId);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        await _client.AdminDeleteUserAsync(new AdminDeleteUserRequest
        {
            UserPoolId = _opt.UserPoolId,
            Username = userId,
        }, ct).ConfigureAwait(false);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        // Self-service: requires the access token, not just a userId.
        // The Idp interface gives us userId only, so use the admin path.
        await _client.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _opt.UserPoolId,
            Username = request.UserId,
            Password = request.NewPassword,
            Permanent = true,
        }, ct).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(string userId, CancellationToken ct = default)
    {
        await _client.AdminResetUserPasswordAsync(new AdminResetUserPasswordRequest
        {
            UserPoolId = _opt.UserPoolId,
            Username = userId,
        }, ct).ConfigureAwait(false);
    }

    public Task<MfaChallengeResponse> MfaChallengeAsync(string userId, CancellationToken ct = default) =>
        // Cognito requires the user to authenticate first (InitiateAuth) before issuing an MFA
        // challenge — the challenge name is returned in the auth response. The Idp port doesn't
        // model that flow; the application is expected to call InitiateAuth itself for MFA.
        throw new NotSupportedException("Cognito MFA is part of the InitiateAuth challenge flow — call InitiateAuthAsync directly with USER_PASSWORD_AUTH and inspect the response's ChallengeName.");

    public Task<TokenResponse> MfaVerifyAsync(MfaVerifyRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Cognito MFA verification flows through RespondToAuthChallengeAsync — call it directly with the SMS_MFA / SOFTWARE_TOKEN_MFA challenge name.");

    public Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(string userId, CancellationToken ct = default) =>
        // Cognito does not expose an admin "list sessions" API. The closest equivalent is
        // AdminListDevicesAsync, but that lists remembered devices, not active sessions.
        throw new NotSupportedException("Cognito has no admin session-listing API. Use AdminListDevicesAsync if you need remembered devices.");

    public async Task RevokeSessionAsync(string userId, string sessionId, CancellationToken ct = default)
    {
        // The closest action is signing the user out everywhere (AdminUserGlobalSignOut),
        // which invalidates *all* sessions — sessionId is therefore ignored.
        await _client.AdminUserGlobalSignOutAsync(new AdminUserGlobalSignOutRequest
        {
            UserPoolId = _opt.UserPoolId,
            Username = userId,
        }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken ct = default)
    {
        var resp = await _client.ListGroupsAsync(new ListGroupsRequest
        {
            UserPoolId = _opt.UserPoolId,
            Limit = 60,
        }, ct).ConfigureAwait(false);

        return resp.Groups?.Select(g => g.GroupName).ToList() ?? new List<string>();
    }

    public async Task<CreateRolesResponse> CreateRolesAsync(CreateRolesRequest request, CancellationToken ct = default)
    {
        var created = new List<string>();
        foreach (var role in request.RoleNames)
        {
            await _client.CreateGroupAsync(new CreateGroupRequest
            {
                UserPoolId = _opt.UserPoolId,
                GroupName = role,
            }, ct).ConfigureAwait(false);
            created.Add(role);
        }

        return new CreateRolesResponse(created);
    }

    public async Task AssignRolesToUserAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        foreach (var role in request.RoleNames)
        {
            await _client.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
            {
                UserPoolId = _opt.UserPoolId,
                Username = request.UserId,
                GroupName = role,
            }, ct).ConfigureAwait(false);
        }
    }

    public async Task RemoveRolesFromUserAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        foreach (var role in request.RoleNames)
        {
            await _client.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest
            {
                UserPoolId = _opt.UserPoolId,
                Username = request.UserId,
                GroupName = role,
            }, ct).ConfigureAwait(false);
        }
    }

    public Task<CreateScopeResponse> CreateScopeAsync(CreateScopeRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Cognito scopes are configured at the resource server level.");
}

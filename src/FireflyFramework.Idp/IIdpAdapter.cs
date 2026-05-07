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

namespace FireflyFramework.Idp;

/// <summary>
/// Identity provider port. Implementations adapt to a concrete IdP (Cognito, Keycloak,
/// Azure AD/Entra ID, internal DB, etc.). Mirrors Java <c>IdpAdapter</c>.
/// </summary>
public interface IIdpAdapter
{
    // Authentication
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);
    Task<IntrospectionResponse> IntrospectAsync(string accessToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    // User Info
    Task<UserInfoResponse> GetUserInfoAsync(string accessToken, CancellationToken ct = default);

    // User Management
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UpdateUserResponse> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteUserAsync(string userId, CancellationToken ct = default);

    // Password
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(string userId, CancellationToken ct = default);

    // MFA
    Task<MfaChallengeResponse> MfaChallengeAsync(string userId, CancellationToken ct = default);
    Task<TokenResponse> MfaVerifyAsync(MfaVerifyRequest request, CancellationToken ct = default);

    // Sessions
    Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(string userId, CancellationToken ct = default);
    Task RevokeSessionAsync(string userId, string sessionId, CancellationToken ct = default);

    // Roles
    Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken ct = default);
    Task<CreateRolesResponse> CreateRolesAsync(CreateRolesRequest request, CancellationToken ct = default);
    Task AssignRolesToUserAsync(AssignRolesRequest request, CancellationToken ct = default);
    Task RemoveRolesFromUserAsync(AssignRolesRequest request, CancellationToken ct = default);

    // Scopes
    Task<CreateScopeResponse> CreateScopeAsync(CreateScopeRequest request, CancellationToken ct = default);

    // Self-service registration (default delegates to CreateUserAsync)
    Task<CreateUserResponse> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct = default) =>
        CreateUserAsync(new CreateUserRequest(request.Username, request.Email, request.Password,
            request.GivenName, request.FamilyName, null, null), ct);
}

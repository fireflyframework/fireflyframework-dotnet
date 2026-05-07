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

public sealed record LoginRequest(string Username, string Password, string? MfaCode = null);
public sealed record TokenResponse(string AccessToken, string? RefreshToken, string TokenType, int ExpiresIn, string? Scope = null, string? IdToken = null);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record IntrospectionResponse(bool Active, string? Username, string? Sub, IReadOnlyList<string>? Roles, IReadOnlyDictionary<string, object?>? Claims);
public sealed record UserInfoResponse(string Sub, string? Email, string? GivenName, string? FamilyName, IReadOnlyList<string>? Roles, IReadOnlyDictionary<string, object?>? Claims);

public sealed record CreateUserRequest(string Username, string Email, string? Password, string? GivenName, string? FamilyName, IReadOnlyList<string>? Roles, IReadOnlyDictionary<string, object?>? Attributes);
public sealed record CreateUserResponse(string UserId);
public sealed record UpdateUserRequest(string UserId, string? Email, string? GivenName, string? FamilyName, IReadOnlyDictionary<string, object?>? Attributes);
public sealed record UpdateUserResponse(string UserId);

public sealed record ChangePasswordRequest(string UserId, string OldPassword, string NewPassword);
public sealed record CreateRolesRequest(IReadOnlyList<string> RoleNames);
public sealed record CreateRolesResponse(IReadOnlyList<string> CreatedRoleIds);
public sealed record AssignRolesRequest(string UserId, IReadOnlyList<string> RoleNames);

public sealed record CreateScopeRequest(string Name, string? Description);
public sealed record CreateScopeResponse(string ScopeId);

public sealed record MfaChallengeResponse(string ChallengeId, string Method);
public sealed record MfaVerifyRequest(string ChallengeId, string Code);

public sealed record SessionInfo(string SessionId, string UserId, DateTimeOffset CreatedAt, DateTimeOffset? LastActivity, string? IpAddress, string? UserAgent);
public sealed record RegisterUserRequest(string Username, string Email, string Password, string? GivenName, string? FamilyName);

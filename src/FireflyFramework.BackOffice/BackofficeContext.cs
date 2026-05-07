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

namespace FireflyFramework.BackOffice;

/// <summary>
/// Immutable context for back-office requests with customer impersonation. Mirrors Java
/// <c>BackofficeContext</c>: identifies the back-office user performing the action, the
/// customer (party) being impersonated, business identifiers (contract / product), and
/// the role/permission sets used for authorisation.
/// </summary>
public sealed record BackofficeContext(
    Guid BackofficeUserId,
    Guid ImpersonatedPartyId,
    Guid? ContractId = null,
    Guid? ProductId = null,
    IReadOnlySet<string>? BackofficeRoles = null,
    IReadOnlySet<string>? BackofficePermissions = null,
    IReadOnlySet<string>? ImpersonatedPartyRoles = null,
    IReadOnlySet<string>? ImpersonatedPartyPermissions = null,
    Guid? TenantId = null,
    DateTimeOffset? ImpersonationStartedAt = null,
    string? ImpersonationReason = null,
    string? BackofficeUserIpAddress = null,
    IReadOnlyDictionary<string, object?>? Attributes = null)
{
    public bool HasBackofficeRole(string role) => BackofficeRoles?.Contains(role) == true;

    public bool HasAnyBackofficeRole(params string[] roles) =>
        BackofficeRoles is not null && roles.Any(BackofficeRoles.Contains);

    public bool HasAllBackofficeRoles(params string[] roles) =>
        BackofficeRoles is not null && roles.All(BackofficeRoles.Contains);

    public bool HasBackofficePermission(string permission) =>
        BackofficePermissions?.Contains(permission) == true;

    public bool ImpersonatedPartyHasRole(string role) =>
        ImpersonatedPartyRoles?.Contains(role) == true;

    public bool HasContract() => ContractId.HasValue;
    public bool HasProduct() => ProductId.HasValue;

    public T? GetAttribute<T>(string key) =>
        Attributes is not null && Attributes.TryGetValue(key, out var v) && v is T t ? t : default;

    public bool IsValidImpersonation() => BackofficeUserId != Guid.Empty && ImpersonatedPartyId != Guid.Empty;
}

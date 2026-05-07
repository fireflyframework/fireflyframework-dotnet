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

namespace FireflyFramework.Cqrs.Context;

/// <summary>Caller / request context. Mirrors Java <c>ExecutionContext</c>.</summary>
public sealed class ExecutionContext
{
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public string? OrganizationId { get; init; }
    public string? SessionId { get; init; }
    public string? RequestId { get; init; }
    public string? Source { get; init; }
    public string? ClientIp { get; init; }
    public string? UserAgent { get; init; }
    public IReadOnlyDictionary<string, bool> FeatureFlags { get; init; } = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public static ExecutionContext Empty { get; } = new();
    public static ExecutionContext System { get; } = new() { UserId = "system", Source = "system" };
}

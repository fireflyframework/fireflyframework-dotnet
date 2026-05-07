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

namespace FireflyFramework.EventSourcing.Tenancy;

/// <summary>
/// Ambient tenant id propagated through the async call chain. The Java implementation
/// uses Reactor's context; the .NET equivalent is <see cref="AsyncLocal{T}"/> which is
/// preserved across <c>async</c>/<c>await</c>, <c>Task.Run</c> and Channels.
/// </summary>
public static class TenantContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public static string? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public static IDisposable BeginScope(string tenantId)
    {
        var prior = _current.Value;
        _current.Value = tenantId;
        return new Scope(prior);
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _prior;
        public Scope(string? prior) => _prior = prior;
        public void Dispose() => _current.Value = _prior;
    }
}

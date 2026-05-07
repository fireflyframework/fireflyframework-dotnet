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

using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Sdk;

/// <summary>
/// Typed contract for the callback management REST API exposed by
/// <c>FireflyFramework.Callbacks.Web</c>. All methods map one-for-one
/// onto the controller surface in <c>CallbackConfigurationController</c>.
/// </summary>
public interface ICallbackClient
{
    Task<IReadOnlyList<CallbackConfigurationDto>?> ListAsync(string? tenantId = null, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> UpdateAsync(Guid id, CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

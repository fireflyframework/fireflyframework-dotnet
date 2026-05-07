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

using FireflyFramework.Webhooks.Interfaces;

namespace FireflyFramework.Webhooks.Sdk;

/// <summary>
/// Typed contract for the inbound-webhook ingestion endpoint exposed by
/// <c>FireflyFramework.Webhooks.Web</c>.
/// </summary>
public interface IWebhookClient
{
    /// <summary>
    /// Posts <paramref name="payload"/> to <c>POST /api/webhooks/{provider}</c>
    /// and returns the framework's <see cref="WebhookResponseDto"/>.
    /// </summary>
    Task<WebhookResponseDto?> SendAsync(string provider, object payload, CancellationToken ct = default);
}

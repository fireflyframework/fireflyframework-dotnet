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

namespace FireflyFramework.Webhooks.Processor;

public sealed record WebhookProcessingContext(WebhookEventDto Event, string ProviderName);

public sealed record WebhookProcessingResult(bool Success, bool ShouldRetry, TimeSpan? RetryAfter, string? Message);

public interface IWebhookProcessor
{
    Task<WebhookProcessingResult> ProcessAsync(WebhookProcessingContext context, CancellationToken ct = default);
    Task BeforeProcessAsync(WebhookProcessingContext context, CancellationToken ct = default) => Task.CompletedTask;
    Task AfterProcessAsync(WebhookProcessingContext context, WebhookProcessingResult result, CancellationToken ct = default) => Task.CompletedTask;
    Task OnErrorAsync(WebhookProcessingContext context, Exception error, CancellationToken ct = default) => Task.CompletedTask;
}

public interface IWebhookSignatureValidator
{
    Task<bool> ValidateSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers, string secret, CancellationToken ct = default);
}

public interface IWebhookIdempotencyService
{
    Task<bool> TryAcquireAsync(string eventId, string provider, TimeSpan ttl, CancellationToken ct = default);
}

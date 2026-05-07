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

namespace FireflyFramework.Callbacks.Interfaces;

public enum CallbackStatus { Active, Paused, Disabled, Failed }
public enum CallbackExecutionStatus { Success, FailedRetrying, FailedPermanent }
public enum CallbackHttpMethod { Post, Put, Patch }

public sealed record CallbackConfigurationDto(
    Guid? Id,
    string Name,
    string Url,
    CallbackHttpMethod HttpMethod,
    CallbackStatus Status,
    string[] SubscribedEventTypes,
    Dictionary<string, string>? CustomHeaders,
    string? Secret,
    bool SignatureEnabled,
    string? SignatureHeader,
    int MaxRetries,
    int RetryDelayMs,
    double RetryBackoffMultiplier,
    int TimeoutMs,
    bool Active,
    string? TenantId,
    string? FilterExpression,
    Dictionary<string, object?>? Metadata,
    int FailureThreshold,
    int FailureCount,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

public sealed record AuthorizedDomainDto(string Domain, string[]? AllowedIPs, bool IsAuthorized);
public sealed record EventSubscriptionDto(Guid ConfigurationId, string EventType, bool IsActive);

public sealed record CallbackExecutionDto(
    Guid Id,
    Guid ConfigurationId,
    string EventType,
    string SourceEventId,
    CallbackExecutionStatus Status,
    string? RequestPayload,
    string? RequestHeaders,
    int? ResponseStatusCode,
    string? ResponseBody,
    int AttemptNumber,
    int MaxAttempts,
    long RequestDurationMs,
    string? ErrorMessage,
    DateTimeOffset ExecutedAt,
    DateTimeOffset? CompletedAt);

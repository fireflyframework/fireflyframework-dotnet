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

namespace FireflyFramework.RuleEngine.Interfaces;

public sealed record RuleDefinitionDto(
    Guid? Id,
    string Code,
    string Name,
    string? Description,
    string YamlContent,
    string Version,
    bool IsActive,
    string[]? ValidationErrors = null);

public sealed record ConstantDto(Guid? Id, string Key, string Value, string? Description, RuleValueType DataType);

public enum RuleValueType { String, Number, Boolean, Json }
public enum ResultType { Success, Failure, Warning }

public enum AuditEventType
{
    RuleDefinitionCreate, RuleDefinitionUpdate, RuleDefinitionDelete,
    RuleEvaluationDirect, RuleEvaluationByCode, RuleEvaluationPlain,
}

public sealed record AuditTrailDto(
    Guid Id,
    AuditEventType OperationType,
    string EntityType,
    string? EntityId,
    string? RuleCode,
    string? UserId,
    string? IpAddress,
    string? UserAgent,
    string? HttpMethod,
    string? Endpoint,
    string? RequestData,
    string? ResponseData,
    int? StatusCode,
    bool Success,
    string? ErrorMessage,
    long? ExecutionTimeMs,
    Dictionary<string, object?>? Metadata,
    string? SessionId,
    string? CorrelationId,
    DateTimeOffset CreatedAt);

public sealed record AuditTrailFilterDto(
    string? RuleCode,
    string? UserId,
    AuditEventType? OperationType,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    int Limit = 100);

public sealed record RulesEvaluationRequestDto(string Base64YamlContent, Dictionary<string, object?> InputData);
public sealed record PlainYamlEvaluationRequestDto(string YamlContent, Dictionary<string, object?> InputData);
public sealed record RuleEvaluationByCodeRequestDto(string RuleCode, Dictionary<string, object?> InputData);

public sealed record RulesEvaluationResponseDto(
    bool Success,
    Dictionary<string, object?> Output,
    long ExecutionTimeMs,
    string? RuleCode,
    Guid? AuditId,
    string? ErrorMessage = null);

public sealed record BatchRulesEvaluationRequestDto(
    List<RulesEvaluationRequestDto> Evaluations,
    int ConcurrencyLimit = 8,
    long TimeoutMs = 30_000);

public sealed record BatchRulesEvaluationResponseDto(
    List<RulesEvaluationResponseDto> Results,
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    long TotalTimeMs,
    int CacheHitCount);

public sealed record ValidateYamlRequest(string YamlContent);
public sealed record ValidationResult(bool IsValid, List<string> Errors);

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

using FireflyFramework.RuleEngine.Interfaces;

namespace FireflyFramework.RuleEngine.Core.Services;

public interface IRulesEvaluationService
{
    Task<RulesEvaluationResponseDto> EvaluateRulesDirectAsync(RulesEvaluationRequestDto request, CancellationToken ct = default);
    Task<RulesEvaluationResponseDto> EvaluateRulesPlainAsync(PlainYamlEvaluationRequestDto request, CancellationToken ct = default);
    Task<RulesEvaluationResponseDto> EvaluateRuleByCodeAsync(RuleEvaluationByCodeRequestDto request, CancellationToken ct = default);
}

public interface IBatchRulesEvaluationService
{
    Task<BatchRulesEvaluationResponseDto> EvaluateBatchAsync(BatchRulesEvaluationRequestDto request, CancellationToken ct = default);
    Task<ValidationResult> ValidateBatchRequestAsync(BatchRulesEvaluationRequestDto request, CancellationToken ct = default);
    Task<BatchStatistics> GetBatchStatisticsAsync(CancellationToken ct = default);
}

public sealed record BatchStatistics(long TotalEvaluations, long SuccessCount, long FailureCount, double AverageTimeMs);

public interface IRuleDefinitionService
{
    Task<RuleDefinitionDto> CreateAsync(RuleDefinitionDto dto, CancellationToken ct = default);
    Task<RuleDefinitionDto> UpdateAsync(RuleDefinitionDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<RuleDefinitionDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<RuleDefinitionDto>> GetAllAsync(CancellationToken ct = default);
    Task<ValidationResult> ValidateAsync(RuleDefinitionDto dto, CancellationToken ct = default);
}

public interface IConstantService
{
    Task<ConstantDto> CreateAsync(ConstantDto dto, CancellationToken ct = default);
    Task<ConstantDto> UpdateAsync(ConstantDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ConstantDto?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<ConstantDto>> GetAllAsync(CancellationToken ct = default);
}

public interface IAuditTrailService
{
    Task<AuditTrailDto> CreateAsync(AuditTrailDto dto, CancellationToken ct = default);
    Task<AuditTrailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AuditTrailDto>> QueryAsync(AuditTrailFilterDto filter, CancellationToken ct = default);
    Task<int> CleanupOldAsync(DateTimeOffset olderThan, CancellationToken ct = default);
}

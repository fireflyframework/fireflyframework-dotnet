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

namespace FireflyFramework.RuleEngine.Sdk;

/// <summary>
/// Typed contract for the rule-engine REST API exposed by
/// <c>FireflyFramework.RuleEngine.Web</c>. All methods map one-for-one
/// onto the <c>RulesEvaluationController</c> surface.
/// </summary>
public interface IRuleEngineClient
{
    /// <summary>POST /api/rules/evaluate/direct — base-64-encoded YAML rule.</summary>
    Task<RulesEvaluationResponseDto?> EvaluateAsync(RulesEvaluationRequestDto request, CancellationToken ct = default);

    /// <summary>POST /api/rules/evaluate/plain — plain-text YAML rule.</summary>
    Task<RulesEvaluationResponseDto?> EvaluatePlainAsync(PlainYamlEvaluationRequestDto request, CancellationToken ct = default);

    /// <summary>POST /api/rules/evaluate/by-code — rule looked up by its stable code.</summary>
    Task<RulesEvaluationResponseDto?> EvaluateByCodeAsync(RuleEvaluationByCodeRequestDto request, CancellationToken ct = default);
}

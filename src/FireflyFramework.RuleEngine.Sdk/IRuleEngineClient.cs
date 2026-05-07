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

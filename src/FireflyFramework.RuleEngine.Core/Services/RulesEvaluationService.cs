using System.Diagnostics;
using System.Text;
using FireflyFramework.RuleEngine.Core.Dsl;
using FireflyFramework.RuleEngine.Core.Engine;
using FireflyFramework.RuleEngine.Interfaces;

namespace FireflyFramework.RuleEngine.Core.Services;

/// <summary>
/// Default <see cref="IRulesEvaluationService"/> implementation: parses YAML, builds
/// the AST, runs the visitor evaluator. Mirrors Java <c>RulesEvaluationServiceImpl</c>.
/// </summary>
public sealed class RulesEvaluationService : IRulesEvaluationService
{
    private readonly IRuleDefinitionService? _definitions;
    private readonly YamlDslParser _parser = new();

    public RulesEvaluationService(IRuleDefinitionService? definitions = null) => _definitions = definitions;

    public Task<RulesEvaluationResponseDto> EvaluateRulesDirectAsync(RulesEvaluationRequestDto request, CancellationToken ct = default)
    {
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(request.Base64YamlContent));
        return Task.FromResult(EvaluateInternal(yaml, request.InputData, ruleCode: null));
    }

    public Task<RulesEvaluationResponseDto> EvaluateRulesPlainAsync(PlainYamlEvaluationRequestDto request, CancellationToken ct = default) =>
        Task.FromResult(EvaluateInternal(request.YamlContent, request.InputData, ruleCode: null));

    public async Task<RulesEvaluationResponseDto> EvaluateRuleByCodeAsync(RuleEvaluationByCodeRequestDto request, CancellationToken ct = default)
    {
        if (_definitions is null)
        {
            return new RulesEvaluationResponseDto(false, new(), 0, request.RuleCode, null, "No rule definition service configured");
        }

        var def = await _definitions.GetByCodeAsync(request.RuleCode, ct).ConfigureAwait(false);
        if (def is null)
        {
            return new RulesEvaluationResponseDto(false, new(), 0, request.RuleCode, null, $"Rule '{request.RuleCode}' not found");
        }

        return EvaluateInternal(def.YamlContent, request.InputData, request.RuleCode);
    }

    private RulesEvaluationResponseDto EvaluateInternal(string yaml, IDictionary<string, object?> input, string? ruleCode)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var ast = _parser.Parse(yaml);
            var ctx = new EvaluationContext();
            var engine = new AstRulesEvaluationEngine(ctx);
            var result = engine.Evaluate(ast, input);
            return new RulesEvaluationResponseDto(
                result.Success,
                result.VariableValues,
                sw.ElapsedMilliseconds,
                ruleCode,
                null,
                result.ErrorMessage);
        }
        catch (Exception ex)
        {
            return new RulesEvaluationResponseDto(false, new(), sw.ElapsedMilliseconds, ruleCode, null, ex.Message);
        }
    }
}

using FireflyFramework.RuleEngine.Core.Services;
using FireflyFramework.RuleEngine.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FireflyFramework.RuleEngine.Web.Controllers;

[ApiController]
[Route("api/rules/evaluate")]
public sealed class RulesEvaluationController : ControllerBase
{
    private readonly IRulesEvaluationService _service;

    public RulesEvaluationController(IRulesEvaluationService service) => _service = service;

    [HttpPost("direct")]
    public Task<RulesEvaluationResponseDto> Direct([FromBody] RulesEvaluationRequestDto request, CancellationToken ct) =>
        _service.EvaluateRulesDirectAsync(request, ct);

    [HttpPost("plain")]
    public Task<RulesEvaluationResponseDto> Plain([FromBody] PlainYamlEvaluationRequestDto request, CancellationToken ct) =>
        _service.EvaluateRulesPlainAsync(request, ct);

    [HttpPost("by-code")]
    public Task<RulesEvaluationResponseDto> ByCode([FromBody] RuleEvaluationByCodeRequestDto request, CancellationToken ct) =>
        _service.EvaluateRuleByCodeAsync(request, ct);
}

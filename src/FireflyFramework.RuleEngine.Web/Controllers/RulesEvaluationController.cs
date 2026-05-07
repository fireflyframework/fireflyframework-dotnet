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

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

using FireflyFramework.RuleEngine.Core.Dsl;
using FireflyFramework.RuleEngine.Core.Engine;
using FluentAssertions;
using Xunit;
using Action = FireflyFramework.RuleEngine.Core.Dsl.Action;

namespace FireflyFramework.Tests;

public class RuleEngineTests
{
    [Fact]
    public void RuleEngine_applies_actions_when_condition_holds()
    {
        var rules = new AstRulesDsl
        {
            RuleName = "discount",
            Conditions = new List<Condition>
            {
                new ComparisonCondition(new VariableExpression("amount"), ComparisonOperator.Gt, new LiteralExpression(100m, LiteralType.Number)),
            },
            Actions = new List<Action>
            {
                new AssignmentAction("discount", AssignmentOperator.Assign, new LiteralExpression(0.1m, LiteralType.Number)),
            },
        };

        var ctx = new EvaluationContext();
        var engine = new AstRulesEvaluationEngine(ctx);
        var result = engine.Evaluate(rules, new Dictionary<string, object?> { ["amount"] = 200m });

        result.Success.Should().BeTrue();
        result.VariableValues["discount"].Should().Be(0.1m);
    }

    [Fact]
    public void RuleEngine_skips_actions_when_condition_fails()
    {
        var rules = new AstRulesDsl
        {
            RuleName = "no-discount",
            Conditions = new List<Condition>
            {
                new ComparisonCondition(new VariableExpression("amount"), ComparisonOperator.Gt, new LiteralExpression(1000m, LiteralType.Number)),
            },
            Actions = new List<Action>
            {
                new AssignmentAction("discount", AssignmentOperator.Assign, new LiteralExpression(0.5m, LiteralType.Number)),
            },
        };

        var engine = new AstRulesEvaluationEngine(new EvaluationContext());
        var result = engine.Evaluate(rules, new Dictionary<string, object?> { ["amount"] = 50m });

        result.Success.Should().BeTrue();
        result.VariableValues.Should().NotContainKey("discount");
    }
}

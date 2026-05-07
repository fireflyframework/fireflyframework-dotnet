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

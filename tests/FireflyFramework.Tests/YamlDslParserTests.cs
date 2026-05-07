using FireflyFramework.RuleEngine.Core.Dsl;
using FireflyFramework.RuleEngine.Core.Engine;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class YamlDslParserTests
{
    [Fact]
    public void Parser_builds_AST_for_simple_rule()
    {
        var yaml = """
ruleName: vip-discount
version: '1'
inputVariables:
  amount: number
  isVip: boolean
conditions:
  - $amount > 500
actions:
  - $discount = 0.15
""";
        var parser = new YamlDslParser();
        var ast = parser.Parse(yaml);
        ast.RuleName.Should().Be("vip-discount");
        ast.Version.Should().Be("1");
        ast.InputVariables.Should().ContainKey("amount").WhoseValue.Should().Be(DslValueType.Number);
        ast.Conditions.Should().HaveCount(1);
        ast.Actions.Should().HaveCount(1);
    }

    [Fact]
    public void Parsed_rule_evaluates_correctly()
    {
        var yaml = """
ruleName: gold-tier
conditions:
  - $balance > 10000
actions:
  - $tier = "gold"
""";
        var ast = new YamlDslParser().Parse(yaml);
        var engine = new AstRulesEvaluationEngine(new EvaluationContext());
        var result = engine.Evaluate(ast, new Dictionary<string, object?> { ["balance"] = 25000m });
        result.Success.Should().BeTrue();
        result.VariableValues["tier"].Should().Be("gold");
    }

    [Fact]
    public void Parser_handles_logical_AND_in_conditions()
    {
        var yaml = """
ruleName: combo
conditions:
  - and:
      - $a > 1
      - $b < 10
actions:
  - $matched = true
""";
        var ast = new YamlDslParser().Parse(yaml);
        var engine = new AstRulesEvaluationEngine(new EvaluationContext());
        var result = engine.Evaluate(ast, new Dictionary<string, object?> { ["a"] = 5m, ["b"] = 3m });
        result.VariableValues["matched"].Should().Be(true);
    }
}

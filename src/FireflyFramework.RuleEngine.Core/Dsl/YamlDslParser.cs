using System.Globalization;
using YamlDotNet.RepresentationModel;
using Action = FireflyFramework.RuleEngine.Core.Dsl.Action;

namespace FireflyFramework.RuleEngine.Core.Dsl;

/// <summary>
/// Parses the Firefly rule YAML DSL into an <see cref="AstRulesDsl"/>. Mirrors Java
/// <c>YamlDslValidator</c> + <c>ASTRulesDSLParser</c>.
/// </summary>
/// <remarks>
/// Schema (subset that maps directly to AST nodes):
/// <code>
/// ruleName: discount-rule
/// version: '1'
/// inputVariables:
///   amount: number
/// conditions:
///   - $amount > 100
/// actions:
///   - $discount = 0.1
/// </code>
/// Conditions and actions can be specified as plain strings (parsed with simple
/// expressions) or as nested objects for richer semantics.
/// </remarks>
public sealed class YamlDslParser
{
    public AstRulesDsl Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();
        stream.Load(reader);
        if (stream.Documents.Count == 0)
        {
            throw new InvalidOperationException("Rule YAML is empty");
        }

        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var rule = new AstRulesDsl
        {
            RuleName = GetScalar(root, "ruleName") ?? "unnamed",
            Version = GetScalar(root, "version") ?? "1",
        };

        if (root.Children.TryGetValue("inputVariables", out var inputs) && inputs is YamlMappingNode inputsMap)
        {
            foreach (var pair in inputsMap.Children)
            {
                rule.InputVariables[((YamlScalarNode)pair.Key).Value!] = ParseDslValueType(((YamlScalarNode)pair.Value).Value!);
            }
        }

        if (root.Children.TryGetValue("conditions", out var conditions) && conditions is YamlSequenceNode condList)
        {
            foreach (var node in condList.Children)
            {
                rule.Conditions.Add(ParseCondition(node));
            }
        }

        if (root.Children.TryGetValue("actions", out var actions) && actions is YamlSequenceNode actList)
        {
            foreach (var node in actList.Children)
            {
                rule.Actions.Add(ParseAction(node));
            }
        }

        return rule;
    }

    private static string? GetScalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(key, out var v) && v is YamlScalarNode scalar ? scalar.Value : null;

    private static DslValueType ParseDslValueType(string raw) => raw.ToLowerInvariant() switch
    {
        "number" or "decimal" or "int" => DslValueType.Number,
        "boolean" or "bool" => DslValueType.Boolean,
        "json" or "object" => DslValueType.Json,
        _ => DslValueType.String,
    };

    private static Condition ParseCondition(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
        {
            return ParseConditionString(scalar.Value!);
        }

        if (node is YamlMappingNode map && map.Children.Count == 1)
        {
            var pair = map.Children.First();
            var op = ((YamlScalarNode)pair.Key).Value!.ToLowerInvariant();
            var children = (YamlSequenceNode)pair.Value;
            var left = ParseCondition(children[0]);
            var right = ParseCondition(children[1]);
            var logicalOp = op switch
            {
                "and" => LogicalOperator.And,
                "or" => LogicalOperator.Or,
                "xor" => LogicalOperator.Xor,
                _ => throw new InvalidOperationException($"Unknown condition operator '{op}'"),
            };

            return new LogicalCondition(left, logicalOp, right);
        }

        throw new InvalidOperationException("Unsupported condition node");
    }

    private static Condition ParseConditionString(string text)
    {
        // Tokens: $var <op> literal
        var tokens = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 3)
        {
            throw new InvalidOperationException($"Cannot parse condition: '{text}'");
        }

        var left = ParseExpression(tokens[0]);
        var op = ParseComparison(tokens[1]);
        var right = ParseExpression(tokens[2]);
        return new ComparisonCondition(left, op, right);
    }

    private static ComparisonOperator ParseComparison(string op) => op switch
    {
        "==" or "=" or "eq" => ComparisonOperator.Eq,
        "!=" or "<>" or "ne" => ComparisonOperator.Ne,
        "<" or "lt" => ComparisonOperator.Lt,
        "<=" or "le" => ComparisonOperator.Le,
        ">" or "gt" => ComparisonOperator.Gt,
        ">=" or "ge" => ComparisonOperator.Ge,
        "contains" => ComparisonOperator.Contains,
        "startsWith" => ComparisonOperator.StartsWith,
        "endsWith" => ComparisonOperator.EndsWith,
        _ => throw new InvalidOperationException($"Unknown comparison operator '{op}'"),
    };

    private static Action ParseAction(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
        {
            return ParseActionString(scalar.Value!);
        }

        throw new InvalidOperationException("Only scalar actions are supported in this parser version");
    }

    private static Action ParseActionString(string text)
    {
        var tokens = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 3 || !tokens[0].StartsWith("$", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot parse action: '{text}'");
        }

        var variable = tokens[0][1..];
        var op = tokens[1] switch
        {
            "=" => AssignmentOperator.Assign,
            "+=" => AssignmentOperator.PlusAssign,
            "-=" => AssignmentOperator.MinusAssign,
            "*=" => AssignmentOperator.MultiplyAssign,
            "/=" => AssignmentOperator.DivideAssign,
            _ => throw new InvalidOperationException($"Unknown assignment operator '{tokens[1]}'"),
        };

        return new AssignmentAction(variable, op, ParseExpression(tokens[2]));
    }

    private static Expression ParseExpression(string token)
    {
        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            return new VariableExpression(token[1..]);
        }

        if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
        {
            return new LiteralExpression(dec, LiteralType.Number);
        }

        if (bool.TryParse(token, out var b))
        {
            return new LiteralExpression(b, LiteralType.Boolean);
        }

        if (token == "null")
        {
            return new LiteralExpression(null, LiteralType.Null);
        }

        var trimmed = token.Trim('"', '\'');
        return new LiteralExpression(trimmed, LiteralType.String);
    }
}

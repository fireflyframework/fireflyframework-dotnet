using System.Globalization;
using FireflyFramework.RuleEngine.Core.Dsl;
using Action = FireflyFramework.RuleEngine.Core.Dsl.Action;

namespace FireflyFramework.RuleEngine.Core.Engine;

/// <summary>
/// Visitor-pattern evaluator for the rule engine AST. Mirrors Java
/// <c>ASTRulesEvaluationEngine</c>: evaluates conditions to determine whether the
/// actions should fire, then executes the action list against an
/// <see cref="EvaluationContext"/>.
/// </summary>
public sealed class AstRulesEvaluationEngine : IAstVisitor<object?>
{
    private readonly EvaluationContext _ctx;

    public AstRulesEvaluationEngine(EvaluationContext context) => _ctx = context;

    public AstRulesEvaluationResult Evaluate(AstRulesDsl rules, IDictionary<string, object?> input)
    {
        try
        {
            foreach (var (k, v) in input)
            {
                _ctx.Variables[k] = v;
            }

            var allConditionsTrue = rules.Conditions.All(c => ToBool(c.Accept(this)));
            if (allConditionsTrue)
            {
                foreach (var action in rules.Actions)
                {
                    action.Accept(this);
                }
            }

            return new AstRulesEvaluationResult(
                true,
                _ctx.Variables.ToDictionary(p => p.Key, p => p.Value),
                _ctx.ExecutedActions,
                _ctx.Variables.ToDictionary(p => p.Key, p => p.Value));
        }
        catch (Exception ex)
        {
            return new AstRulesEvaluationResult(false, new(), _ctx.ExecutedActions, new(), ex.Message);
        }
    }

    public object? VisitBinary(BinaryExpression node)
    {
        var l = node.Left.Accept(this);
        var r = node.Right.Accept(this);
        return node.Operator switch
        {
            BinaryOperator.Plus => Add(l, r),
            BinaryOperator.Minus => ToDecimal(l) - ToDecimal(r),
            BinaryOperator.Multiply => ToDecimal(l) * ToDecimal(r),
            BinaryOperator.Divide => ToDecimal(l) / ToDecimal(r),
            BinaryOperator.Modulo => ToDecimal(l) % ToDecimal(r),
            _ => null,
        };
    }

    public object? VisitUnary(UnaryExpression node)
    {
        var v = node.Operand.Accept(this);
        return node.Operator switch
        {
            UnaryOperator.Not => !ToBool(v),
            UnaryOperator.Negate => -ToDecimal(v),
            _ => null,
        };
    }

    public object? VisitVariable(VariableExpression node) => _ctx.Get(node.Name);

    public object? VisitLiteral(LiteralExpression node) => node.Value;

    public object? VisitFunctionCall(FunctionCallExpression node) => null; // pluggable via registry

    public object? VisitArithmetic(ArithmeticExpression node) => node.Operation switch
    {
        ArithmeticOperation.Add => Add(node.Left.Accept(this), node.Right.Accept(this)),
        ArithmeticOperation.Subtract => ToDecimal(node.Left.Accept(this)) - ToDecimal(node.Right.Accept(this)),
        ArithmeticOperation.Multiply => ToDecimal(node.Left.Accept(this)) * ToDecimal(node.Right.Accept(this)),
        ArithmeticOperation.Divide => ToDecimal(node.Left.Accept(this)) / ToDecimal(node.Right.Accept(this)),
        _ => null,
    };

    public object? VisitComparison(ComparisonCondition node)
    {
        var l = node.Left.Accept(this);
        var r = node.Right.Accept(this);
        return node.Operator switch
        {
            ComparisonOperator.Eq => Equals(l, r),
            ComparisonOperator.Ne => !Equals(l, r),
            ComparisonOperator.Lt => ToDecimal(l) < ToDecimal(r),
            ComparisonOperator.Le => ToDecimal(l) <= ToDecimal(r),
            ComparisonOperator.Gt => ToDecimal(l) > ToDecimal(r),
            ComparisonOperator.Ge => ToDecimal(l) >= ToDecimal(r),
            ComparisonOperator.Contains => l?.ToString()?.Contains(r?.ToString() ?? string.Empty, StringComparison.Ordinal) ?? false,
            ComparisonOperator.StartsWith => l?.ToString()?.StartsWith(r?.ToString() ?? string.Empty, StringComparison.Ordinal) ?? false,
            ComparisonOperator.EndsWith => l?.ToString()?.EndsWith(r?.ToString() ?? string.Empty, StringComparison.Ordinal) ?? false,
            _ => false,
        };
    }

    public object? VisitLogical(LogicalCondition node) => node.Operator switch
    {
        LogicalOperator.And => ToBool(node.Left.Accept(this)) && ToBool(node.Right.Accept(this)),
        LogicalOperator.Or => ToBool(node.Left.Accept(this)) || ToBool(node.Right.Accept(this)),
        LogicalOperator.Xor => ToBool(node.Left.Accept(this)) ^ ToBool(node.Right.Accept(this)),
        _ => false,
    };

    public object? VisitExpressionCondition(ExpressionCondition node) => ToBool(node.Expression.Accept(this));

    public object? VisitAssignment(AssignmentAction node)
    {
        var value = node.Value.Accept(this);
        var current = _ctx.Get(node.Variable);
        var next = node.Operator switch
        {
            AssignmentOperator.Assign => value,
            AssignmentOperator.PlusAssign => Add(current, value),
            AssignmentOperator.MinusAssign => ToDecimal(current) - ToDecimal(value),
            AssignmentOperator.MultiplyAssign => ToDecimal(current) * ToDecimal(value),
            AssignmentOperator.DivideAssign => ToDecimal(current) / ToDecimal(value),
            _ => value,
        };
        _ctx.Set(node.Variable, next);
        _ctx.ExecutedActions.Add($"assign {node.Variable}");
        return null;
    }

    public object? VisitFunctionCallAction(FunctionCallAction node)
    {
        _ctx.ExecutedActions.Add($"call {node.Name}");
        return null;
    }

    public object? VisitConditional(ConditionalAction node)
    {
        var branch = ToBool(node.Condition.Accept(this)) ? node.ThenBranch : node.ElseBranch;
        foreach (var a in branch) a.Accept(this);
        return null;
    }

    public object? VisitForEach(ForEachAction node)
    {
        if (node.Collection.Accept(this) is not System.Collections.IEnumerable enumerable) return null;
        foreach (var item in enumerable)
        {
            _ctx.Set(node.ItemVariable, item);
            foreach (var a in node.Body) a.Accept(this);
        }

        return null;
    }

    public object? VisitWhile(WhileAction node)
    {
        var iterations = 0;
        while (ToBool(node.Condition.Accept(this)))
        {
            if (++iterations > 10_000) throw new InvalidOperationException("Loop limit reached");
            foreach (var a in node.Body) a.Accept(this);
        }

        return null;
    }

    private static bool ToBool(object? v) => v is bool b ? b : v is not null && !Equals(v, 0);

    private static decimal ToDecimal(object? v) => v switch
    {
        null => 0,
        decimal d => d,
        double d => (decimal)d,
        float f => (decimal)f,
        int i => i,
        long l => l,
        string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) => p,
        _ => 0,
    };

    private static object Add(object? l, object? r) => l switch
    {
        string s => s + (r?.ToString() ?? string.Empty),
        _ => ToDecimal(l) + ToDecimal(r),
    };
}

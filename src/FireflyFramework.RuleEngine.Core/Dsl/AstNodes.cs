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

namespace FireflyFramework.RuleEngine.Core.Dsl;

public sealed record SourceLocation(int Line, int Column, string? FileName);

public abstract class AstNode
{
    public SourceLocation? Location { get; init; }
    public abstract string NodeType { get; }
    public abstract T Accept<T>(IAstVisitor<T> visitor);
}

public interface IAstVisitor<out T>
{
    T VisitBinary(BinaryExpression node);
    T VisitUnary(UnaryExpression node);
    T VisitVariable(VariableExpression node);
    T VisitLiteral(LiteralExpression node);
    T VisitFunctionCall(FunctionCallExpression node);
    T VisitArithmetic(ArithmeticExpression node);
    T VisitComparison(ComparisonCondition node);
    T VisitLogical(LogicalCondition node);
    T VisitExpressionCondition(ExpressionCondition node);
    T VisitAssignment(AssignmentAction node);
    T VisitFunctionCallAction(FunctionCallAction node);
    T VisitConditional(ConditionalAction node);
    T VisitForEach(ForEachAction node);
    T VisitWhile(WhileAction node);
}

public abstract class Expression : AstNode { }
public abstract class Condition : AstNode { }
public abstract class Action : AstNode { }

public enum BinaryOperator { Plus, Minus, Multiply, Divide, Modulo }
public enum UnaryOperator { Not, Negate }
public enum LiteralType { String, Number, Boolean, Null }
public enum ArithmeticOperation { Add, Subtract, Multiply, Divide }
public enum ComparisonOperator { Eq, Ne, Lt, Le, Gt, Ge, Like, In, Contains, StartsWith, EndsWith }
public enum LogicalOperator { And, Or, Xor }
public enum AssignmentOperator { Assign, PlusAssign, MinusAssign, MultiplyAssign, DivideAssign }

public sealed class BinaryExpression(Expression left, BinaryOperator op, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public BinaryOperator Operator { get; } = op;
    public Expression Right { get; } = right;
    public override string NodeType => "Binary";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBinary(this);
}

public sealed class UnaryExpression(UnaryOperator op, Expression operand) : Expression
{
    public UnaryOperator Operator { get; } = op;
    public Expression Operand { get; } = operand;
    public override string NodeType => "Unary";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitUnary(this);
}

public sealed class VariableExpression(string name) : Expression
{
    public string Name { get; } = name;
    public override string NodeType => "Variable";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitVariable(this);
}

public sealed class LiteralExpression(object? value, LiteralType type) : Expression
{
    public object? Value { get; } = value;
    public LiteralType Type { get; } = type;
    public override string NodeType => "Literal";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLiteral(this);
}

public sealed class FunctionCallExpression(string name, IReadOnlyList<Expression> args) : Expression
{
    public string Name { get; } = name;
    public IReadOnlyList<Expression> Arguments { get; } = args;
    public override string NodeType => "FunctionCall";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionCall(this);
}

public sealed class ArithmeticExpression(Expression left, ArithmeticOperation op, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public ArithmeticOperation Operation { get; } = op;
    public Expression Right { get; } = right;
    public override string NodeType => "Arithmetic";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitArithmetic(this);
}

public sealed class ComparisonCondition(Expression left, ComparisonOperator op, Expression right) : Condition
{
    public Expression Left { get; } = left;
    public ComparisonOperator Operator { get; } = op;
    public Expression Right { get; } = right;
    public override string NodeType => "Comparison";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitComparison(this);
}

public sealed class LogicalCondition(Condition left, LogicalOperator op, Condition right) : Condition
{
    public Condition Left { get; } = left;
    public LogicalOperator Operator { get; } = op;
    public Condition Right { get; } = right;
    public override string NodeType => "Logical";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLogical(this);
}

public sealed class ExpressionCondition(Expression expression) : Condition
{
    public Expression Expression { get; } = expression;
    public override string NodeType => "ExpressionCondition";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitExpressionCondition(this);
}

public sealed class AssignmentAction(string variable, AssignmentOperator op, Expression value) : Action
{
    public string Variable { get; } = variable;
    public AssignmentOperator Operator { get; } = op;
    public Expression Value { get; } = value;
    public override string NodeType => "Assignment";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitAssignment(this);
}

public sealed class FunctionCallAction(string name, IReadOnlyList<Expression> args) : Action
{
    public string Name { get; } = name;
    public IReadOnlyList<Expression> Arguments { get; } = args;
    public override string NodeType => "FunctionCallAction";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionCallAction(this);
}

public sealed class ConditionalAction(Condition condition, IReadOnlyList<Action> thenBranch, IReadOnlyList<Action> elseBranch) : Action
{
    public Condition Condition { get; } = condition;
    public IReadOnlyList<Action> ThenBranch { get; } = thenBranch;
    public IReadOnlyList<Action> ElseBranch { get; } = elseBranch;
    public override string NodeType => "Conditional";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitConditional(this);
}

public sealed class ForEachAction(string item, Expression collection, IReadOnlyList<Action> body) : Action
{
    public string ItemVariable { get; } = item;
    public Expression Collection { get; } = collection;
    public IReadOnlyList<Action> Body { get; } = body;
    public override string NodeType => "ForEach";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitForEach(this);
}

public sealed class WhileAction(Condition condition, IReadOnlyList<Action> body) : Action
{
    public Condition Condition { get; } = condition;
    public IReadOnlyList<Action> Body { get; } = body;
    public override string NodeType => "While";
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitWhile(this);
}

public sealed class AstRulesDsl
{
    public string RuleName { get; init; } = string.Empty;
    public string Version { get; init; } = "1";
    public Dictionary<string, DslValueType> InputVariables { get; init; } = new();
    public List<Condition> Conditions { get; init; } = new();
    public List<Action> Actions { get; init; } = new();
}

public enum DslValueType { String, Number, Boolean, Json }

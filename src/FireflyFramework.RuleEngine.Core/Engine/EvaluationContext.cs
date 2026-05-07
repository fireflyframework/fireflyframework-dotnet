namespace FireflyFramework.RuleEngine.Core.Engine;

public sealed class EvaluationContext
{
    public Dictionary<string, object?> Variables { get; } = new();
    public Dictionary<string, object?> Constants { get; } = new();
    public List<string> ExecutedActions { get; } = new();

    public object? Get(string name)
    {
        if (Variables.TryGetValue(name, out var v)) return v;
        if (Constants.TryGetValue(name, out var c)) return c;
        return null;
    }

    public void Set(string name, object? value) => Variables[name] = value;
}

public sealed record AstRulesEvaluationResult(
    bool Success,
    Dictionary<string, object?> Output,
    List<string> ExecutedActions,
    Dictionary<string, object?> VariableValues,
    string? ErrorMessage = null);

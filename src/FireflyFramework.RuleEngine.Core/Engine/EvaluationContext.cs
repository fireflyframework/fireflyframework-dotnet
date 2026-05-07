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

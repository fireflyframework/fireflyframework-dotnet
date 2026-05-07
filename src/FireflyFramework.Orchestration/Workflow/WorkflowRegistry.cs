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

using System.Collections.Concurrent;
using System.Reflection;

namespace FireflyFramework.Orchestration.Workflow;

/// <summary>
/// Registry of <see cref="WorkflowAttribute"/>-annotated workflow types. Mirrors Java
/// <c>WorkflowRegistry</c>. The REST controller looks up workflow types by their declared
/// <c>Id</c>; consumer applications register workflow types at startup (typically via
/// <c>AddFireflyWorkflows(typeof(MyAssembly))</c>).
/// </summary>
public sealed class WorkflowRegistry
{
    private readonly ConcurrentDictionary<string, Type> _byId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers <paramref name="workflowType"/>. The type must be decorated with
    /// <see cref="WorkflowAttribute"/>; the <see cref="WorkflowAttribute.Id"/> becomes the
    /// registry key. Re-registering the same id replaces the previous entry.
    /// </summary>
    public void Register(Type workflowType)
    {
        ArgumentNullException.ThrowIfNull(workflowType);
        var attr = workflowType.GetCustomAttribute<WorkflowAttribute>()
            ?? throw new InvalidOperationException($"{workflowType.Name} is not annotated with [Workflow].");
        _byId[attr.Id] = workflowType;
    }

    /// <summary>Convenience overload — <c>Register&lt;MyWorkflow&gt;()</c>.</summary>
    public void Register<T>() where T : class => Register(typeof(T));

    /// <summary>
    /// Scans <paramref name="assembly"/> for every concrete class decorated with
    /// <see cref="WorkflowAttribute"/> and registers them.
    /// </summary>
    public int RegisterFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var count = 0;
        foreach (var type in assembly.GetTypes())
        {
            if (type is { IsClass: true, IsAbstract: false } && type.GetCustomAttribute<WorkflowAttribute>() is not null)
            {
                Register(type);
                count++;
            }
        }
        return count;
    }

    /// <summary>Resolves a workflow type by id, or <c>null</c> if unknown.</summary>
    public Type? Find(string workflowId) => _byId.TryGetValue(workflowId, out var type) ? type : null;

    /// <summary>Returns a descriptor for every registered workflow.</summary>
    public IReadOnlyList<WorkflowDescriptor> GetAll() => _byId.Values.Select(Describe).ToList();

    /// <summary>Returns a descriptor for one workflow, or <c>null</c> if unknown.</summary>
    public WorkflowDescriptor? Describe(string workflowId) => Find(workflowId) is { } type ? Describe(type) : null;

    private static WorkflowDescriptor Describe(Type type)
    {
        var attr = type.GetCustomAttribute<WorkflowAttribute>()!;
        var steps = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.GetCustomAttribute<WorkflowStepAttribute>())
            .Where(a => a is not null)
            .Select(a => new StepDescriptor(a!.Id, a.Name ?? a.Id))
            .ToList();
        return new WorkflowDescriptor(attr.Id, attr.Name ?? attr.Id, attr.Description, attr.Version, steps);
    }
}

/// <summary>Describes one registered workflow.</summary>
public sealed record WorkflowDescriptor(
    string Id,
    string Name,
    string? Description,
    string Version,
    IReadOnlyList<StepDescriptor> Steps);

/// <summary>Describes one step in a registered workflow.</summary>
public sealed record StepDescriptor(string Id, string Name);

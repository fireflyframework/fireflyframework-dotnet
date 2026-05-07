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

using FireflyFramework.Orchestration.Core;

namespace FireflyFramework.Orchestration.Workflow;

[AttributeUsage(AttributeTargets.Class)]
public sealed class WorkflowAttribute : Attribute
{
    public WorkflowAttribute(string id) => Id = id;
    public string Id { get; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string Version { get; set; } = "1";
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Sync;
    public string? TriggerEventType { get; set; }
    public int TimeoutMs { get; set; } = 600_000;
    public int MaxRetries { get; set; }
    public int RetryDelayMs { get; set; } = 1_000;
    public bool PublishEvents { get; set; } = true;
    public int LayerConcurrency { get; set; } = 1;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class WorkflowStepAttribute : Attribute
{
    public WorkflowStepAttribute(string id) => Id = id;
    public string Id { get; }
    public string? Name { get; set; }
    public int Timeout { get; set; } = 30_000;
    public int Retries { get; set; }
    public int RetryDelay { get; set; } = 1_000;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class WaitForSignalAttribute : Attribute
{
    public WaitForSignalAttribute(string name) => Name = name;
    public string Name { get; }
    public int TimeoutMs { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class WaitForTimerAttribute : Attribute
{
    public WaitForTimerAttribute(int durationMs) => DurationMs = durationMs;
    public int DurationMs { get; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ChildWorkflowAttribute : Attribute
{
    public ChildWorkflowAttribute(string name) => Name = name;
    public string Name { get; }
    public int Timeout { get; set; } = 60_000;
    public int Retries { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class WorkflowQueryAttribute : Attribute
{
    public WorkflowQueryAttribute(string name) => Name = name;
    public string Name { get; }
}

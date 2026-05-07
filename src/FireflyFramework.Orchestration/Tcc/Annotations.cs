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

namespace FireflyFramework.Orchestration.Tcc;

[AttributeUsage(AttributeTargets.Class)]
public sealed class TccAttribute : Attribute
{
    public TccAttribute(string name) => Name = name;
    public string Name { get; }
    public int TimeoutMs { get; set; } = 60_000;
    public bool RetryEnabled { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int BackoffMs { get; set; } = 200;
    public string? TriggerEventType { get; set; }
}

[AttributeUsage(AttributeTargets.Class)] public sealed class TccParticipantAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)] public sealed class TryMethodAttribute : Attribute { public int TimeoutMs { get; set; } = 30_000; public int Retry { get; set; } public int BackoffMs { get; set; } = 100; }
[AttributeUsage(AttributeTargets.Method)] public sealed class ConfirmMethodAttribute : Attribute { public int TimeoutMs { get; set; } = 30_000; public int Retry { get; set; } public int BackoffMs { get; set; } = 100; }
[AttributeUsage(AttributeTargets.Method)] public sealed class CancelMethodAttribute : Attribute { public int TimeoutMs { get; set; } = 30_000; public int Retry { get; set; } public int BackoffMs { get; set; } = 100; }
[AttributeUsage(AttributeTargets.Parameter)] public sealed class FromTryAttribute : Attribute { public string Source { get; set; } = string.Empty; }

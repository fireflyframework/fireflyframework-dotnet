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

namespace FireflyFramework.Orchestration.Workflow;

/// <summary>
/// Workflow timer abstraction. Mirrors Java <c>TimerService</c>. The default
/// implementation simply uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>; a
/// production implementation can persist timers and survive process restarts.
/// </summary>
public class TimerService
{
    public virtual Task SleepAsync(TimeSpan duration, CancellationToken ct = default) =>
        Task.Delay(duration, ct);
}

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

using System.Runtime.CompilerServices;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Publisher;

namespace FireflyFramework.Eda.Consumer;

public sealed class InMemoryEventConsumer : IEventConsumer
{
    private readonly InMemoryEventBus _bus;
    private bool _running;

    public InMemoryEventConsumer(InMemoryEventBus bus) => _bus = bus;

    public ConsumerType Type => ConsumerType.InMemory;
    public bool IsRunning => _running;
    public bool IsAvailable => true;

    public Task StartAsync(CancellationToken ct = default) { _running = true; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct = default) { _running = false; return Task.CompletedTask; }

    public async IAsyncEnumerable<EventEnvelope> ConsumeAsync(
        IEnumerable<string> destinations,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = destinations.Select(d => _bus.Channel(d)).ToList();
        while (!ct.IsCancellationRequested)
        {
            foreach (var ch in queue)
            {
                while (ch.Reader.TryRead(out var env))
                {
                    yield return env;
                }
            }

            await Task.Delay(50, ct).ConfigureAwait(false);
        }
    }

    public Task<ConsumerHealth> GetHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(new ConsumerHealth(Type, true, _running, "UP"));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

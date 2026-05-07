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

using System.Threading.Channels;
using FireflyFramework.Webhooks.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Webhooks.Core.Services;

public sealed class BatchingOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 100;
    public TimeSpan BatchWindow { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Buffers incoming webhook events for micro-batching downstream (Kafka, persistence,
/// projection). Mirrors Java <c>WebhookBatchingService</c>.
/// </summary>
public sealed class WebhookBatchingService : IAsyncDisposable
{
    private readonly Channel<WebhookEventDto> _channel;
    private readonly BatchingOptions _options;
    private readonly ILogger<WebhookBatchingService> _log;

    public WebhookBatchingService(IOptions<BatchingOptions> options, ILogger<WebhookBatchingService> log)
    {
        _options = options.Value;
        _log = log;
        _channel = Channel.CreateUnbounded<WebhookEventDto>();
    }

    public ValueTask EnqueueAsync(WebhookEventDto evt, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(evt, ct);

    public async IAsyncEnumerable<IReadOnlyList<WebhookEventDto>> ReadBatchesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var batch = new List<WebhookEventDto>(_options.BatchSize);
        DateTimeOffset? batchStartedAt = null;

        while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var evt))
            {
                if (batch.Count == 0) batchStartedAt = DateTimeOffset.UtcNow;
                batch.Add(evt);

                if (batch.Count >= _options.BatchSize ||
                    (batchStartedAt is { } start && DateTimeOffset.UtcNow - start > _options.BatchWindow))
                {
                    yield return batch.ToList();
                    batch.Clear();
                    batchStartedAt = null;
                }
            }

            if (batch.Count > 0)
            {
                yield return batch.ToList();
                batch.Clear();
                batchStartedAt = null;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

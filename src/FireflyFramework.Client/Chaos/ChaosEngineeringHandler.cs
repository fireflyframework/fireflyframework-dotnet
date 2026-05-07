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

namespace FireflyFramework.Client.Chaos;

/// <summary>
/// HTTP-pipeline <see cref="DelegatingHandler"/> that injects faults per
/// <see cref="FaultInjectionConfig"/>. Mirrors Java <c>ChaosEngineeringInterceptor</c>.
///
/// <para>Wire it into an <see cref="HttpClient"/> via
/// <c>HttpClientBuilderExtensions.AddHttpMessageHandler</c>. Each request rolls the dice
/// against the configured probabilities; the configured service name is read from a
/// <c>"firefly.chaos.service"</c> request property (set by callers / interceptors upstream)
/// so the same handler can serve multiple downstream services with different policies.</para>
/// </summary>
public sealed class ChaosEngineeringHandler : DelegatingHandler
{
    /// <summary>HTTP-request property key used to identify the downstream service for chaos rules.</summary>
    public const string ServiceNameOption = "firefly.chaos.service";

    private readonly FaultInjectionConfig _config;
    private readonly Random _rng;

    public ChaosEngineeringHandler(FaultInjectionConfig config, Random? rng = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rng = rng ?? Random.Shared;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var serviceName = ResolveServiceName(request);
        if (_config.AppliesTo(serviceName))
        {
            // Order matters: error injection short-circuits the pipeline; latency runs before
            // the request goes out (mimics network slowdown); timeout adds to that latency.
            if (_config.ErrorInjectionEnabled && _rng.NextDouble() < _config.ErrorProbability)
            {
                throw _config.ErrorFactory();
            }

            if (_config.LatencyInjectionEnabled && _rng.NextDouble() < _config.LatencyProbability)
            {
                var delay = NextLatency();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (_config.TimeoutInjectionEnabled && _rng.NextDouble() < _config.TimeoutProbability)
            {
                await Task.Delay(_config.TimeoutDelay, cancellationToken).ConfigureAwait(false);
                throw new TimeoutException("Chaos timeout injection");
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private TimeSpan NextLatency()
    {
        var min = _config.MinLatency.TotalMilliseconds;
        var max = _config.MaxLatency.TotalMilliseconds;
        if (max <= min) return TimeSpan.FromMilliseconds(min);
        return TimeSpan.FromMilliseconds(min + (_rng.NextDouble() * (max - min)));
    }

    private static string ResolveServiceName(HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(new HttpRequestOptionsKey<string>(ServiceNameOption), out var name))
        {
            return name;
        }
        return request.RequestUri?.Host ?? "unknown";
    }
}

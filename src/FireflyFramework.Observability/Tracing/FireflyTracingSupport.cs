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

using System.Diagnostics;

namespace FireflyFramework.Observability.Tracing;

/// <summary>
/// Helper for creating spans around async operations. Mirrors Java
/// <c>FireflyTracingSupport</c>; uses <see cref="ActivitySource"/> which automatically
/// propagates context across <c>async</c>/<c>await</c> boundaries.
/// </summary>
public sealed class FireflyTracingSupport
{
    private readonly ActivitySource _source;

    public FireflyTracingSupport(string sourceName) => _source = new ActivitySource(sourceName);

    public ActivitySource Source => _source;

    public async Task<T> Trace<T>(string spanName, Func<Task<T>> work, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = _source.StartActivity(spanName, ActivityKind.Internal);
        if (activity is not null && tags is not null)
        {
            foreach (var t in tags)
            {
                activity.SetTag(t.Key, t.Value);
            }
        }

        try
        {
            var result = await work().ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            throw;
        }
    }
}

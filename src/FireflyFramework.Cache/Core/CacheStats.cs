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

namespace FireflyFramework.Cache.Core;

/// <summary>Cache statistics. Mirrors Java <c>CacheStats</c>.</summary>
public sealed record CacheStats(
    CacheType Type,
    string Name,
    long RequestCount,
    long HitCount,
    long MissCount,
    long LoadCount,
    long EvictionCount,
    long EntryCount,
    TimeSpan AverageLoadTime,
    long EstimatedSizeBytes,
    DateTimeOffset CapturedAt)
{
    public double HitRate => RequestCount == 0 ? 0 : (double)HitCount / RequestCount;
    public double MissRate => RequestCount == 0 ? 0 : (double)MissCount / RequestCount;

    public static CacheStats Empty(CacheType type, string name) => new(
        type, name, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, 0, DateTimeOffset.UtcNow);
}

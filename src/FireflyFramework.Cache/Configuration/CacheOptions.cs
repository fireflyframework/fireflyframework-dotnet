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

using FireflyFramework.Cache.Core;

namespace FireflyFramework.Cache.Configuration;

/// <summary>Configuration root for the cache module. Mirrors Java <c>CacheProperties</c>.</summary>
public sealed class FireflyCacheOptions
{
    public const string SectionName = "Firefly:Cache";

    public CacheType Provider { get; set; } = CacheType.Auto;
    public string Name { get; set; } = "default";
    public string KeyPrefix { get; set; } = "firefly:cache:";
    public RedisCacheOptions Redis { get; set; } = new();
    public MemoryCacheOptions Memory { get; set; } = new();
}

public sealed class RedisCacheOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public TimeSpan? DefaultTtl { get; set; }
}

public sealed class MemoryCacheOptions
{
    public long? SizeLimit { get; set; }
}

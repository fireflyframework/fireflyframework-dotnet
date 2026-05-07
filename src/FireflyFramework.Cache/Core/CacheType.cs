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

/// <summary>Cache backend kind. Mirrors Java <c>CacheType</c>.</summary>
public enum CacheType
{
    Memory,
    Redis,
    Hazelcast,
    JCache,
    NoOp,
    Auto,
}

public static class CacheTypeExtensions
{
    public static bool IsDistributed(this CacheType type) =>
        type is CacheType.Redis or CacheType.Hazelcast or CacheType.JCache;

    public static bool SupportsPersistence(this CacheType type) =>
        type is CacheType.Redis or CacheType.Hazelcast;

    public static string DisplayName(this CacheType type) => type switch
    {
        CacheType.Memory => "Memory",
        CacheType.Redis => "Redis",
        CacheType.Hazelcast => "Hazelcast",
        CacheType.JCache => "JCache",
        CacheType.NoOp => "NoOp",
        CacheType.Auto => "Auto",
        _ => type.ToString(),
    };
}

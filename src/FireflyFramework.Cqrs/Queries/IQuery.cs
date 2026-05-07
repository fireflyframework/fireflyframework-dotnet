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

using FireflyFramework.Cqrs.Authorization;
using FireflyFramework.Cqrs.Context;

namespace FireflyFramework.Cqrs.Queries;

/// <summary>
/// Marker for read-side messages. Mirrors Java <c>Query&lt;R&gt;</c>. Set
/// <see cref="IsCacheable"/> = true to opt into the QueryBus result cache.
/// </summary>
public interface IQuery<out TResult>
{
    bool IsCacheable => false;
    string? CacheKey => null;
    TimeSpan? CacheTtl => null;

    Task<AuthorizationResult> AuthorizeAsync(ExecutionContext context, CancellationToken ct = default) => Task.FromResult(AuthorizationResult.Allowed());
}

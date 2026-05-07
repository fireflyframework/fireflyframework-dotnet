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

using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Queries;

namespace FireflyFramework.Cqrs.Buses;

public interface IQueryBus
{
    Task<TResult> AskAsync<TResult>(IQuery<TResult> query, ExecutionContext context, CancellationToken ct = default);

    Task<TResult> AskAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        AskAsync(query, ExecutionContext.Empty, ct);

    Task ClearCacheAsync(string? pattern = null, CancellationToken ct = default);
}

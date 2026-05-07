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

using System.Collections.Concurrent;

namespace FireflyFramework.Cqrs.Buses;

/// <summary>
/// Maps a request type (command or query) to a delegate that invokes its handler.
/// Mirrors Java <c>CommandHandlerRegistry</c> / <c>QueryHandlerRegistry</c>.
/// </summary>
public sealed class HandlerRegistry<TInvoker>
{
    private readonly ConcurrentDictionary<Type, TInvoker> _map = new();

    public void Register(Type requestType, TInvoker invoker)
    {
        if (!_map.TryAdd(requestType, invoker))
        {
            throw new InvalidOperationException(
                $"Handler for {requestType.FullName} is already registered");
        }
    }

    public bool Has(Type requestType) => _map.ContainsKey(requestType);

    public TInvoker Get(Type requestType) => _map.TryGetValue(requestType, out var i)
        ? i
        : throw new InvalidOperationException($"No handler registered for {requestType.FullName}");
}

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

using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Queries;

namespace FireflyFramework.Cqrs.Buses;

/// <summary>
/// Fluent helper for dispatching commands to the bus with a builder-style API.
/// Mirrors Java <c>CommandBuilder</c>.
/// </summary>
/// <example>
/// <code>
/// var orderId = await commandBus.For(new CreateOrder(...))
///     .WithUser("alice")
///     .WithCorrelation(corrId)
///     .ExecuteAsync(ct);
/// </code>
/// </example>
public sealed class CommandFluent<TResult>
{
    private readonly ICommandBus _bus;
    private readonly ICommand<TResult> _command;
    private string? _userId;
    private string? _correlationId;
    private readonly Dictionary<string, object?> _attributes = new();

    public CommandFluent(ICommandBus bus, ICommand<TResult> command)
    {
        _bus = bus;
        _command = command;
    }

    public CommandFluent<TResult> WithUser(string userId) { _userId = userId; return this; }
    public CommandFluent<TResult> WithCorrelation(string correlationId) { _correlationId = correlationId; return this; }
    public CommandFluent<TResult> WithAttribute(string key, object? value) { _attributes[key] = value; return this; }

    public Task<TResult> ExecuteAsync(CancellationToken ct = default)
    {
        var ctx = new ExecutionContext { UserId = _userId, RequestId = _correlationId, Properties = _attributes };
        return _bus.SendAsync(_command, ctx, ct);
    }
}

/// <summary>Fluent helper for dispatching queries. Mirrors Java <c>QueryBuilder</c>.</summary>
public sealed class QueryFluent<TResult>
{
    private readonly IQueryBus _bus;
    private readonly IQuery<TResult> _query;
    private string? _userId;
    private string? _correlationId;
    private readonly Dictionary<string, object?> _attributes = new();

    public QueryFluent(IQueryBus bus, IQuery<TResult> query)
    {
        _bus = bus;
        _query = query;
    }

    public QueryFluent<TResult> WithUser(string userId) { _userId = userId; return this; }
    public QueryFluent<TResult> WithCorrelation(string correlationId) { _correlationId = correlationId; return this; }
    public QueryFluent<TResult> WithAttribute(string key, object? value) { _attributes[key] = value; return this; }

    public Task<TResult> ExecuteAsync(CancellationToken ct = default)
    {
        var ctx = new ExecutionContext { UserId = _userId, RequestId = _correlationId, Properties = _attributes };
        return _bus.AskAsync(_query, ctx, ct);
    }
}

public static class CommandBusExtensions
{
    public static CommandFluent<TResult> For<TResult>(this ICommandBus bus, ICommand<TResult> command) =>
        new(bus, command);
}

public static class QueryBusExtensions
{
    public static QueryFluent<TResult> For<TResult>(this IQueryBus bus, IQuery<TResult> query) =>
        new(bus, query);
}

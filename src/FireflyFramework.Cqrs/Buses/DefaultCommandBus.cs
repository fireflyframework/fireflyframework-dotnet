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
using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Validation;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Cqrs.Buses;

public sealed class DefaultCommandBus : ICommandBus
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<DefaultCommandBus> _log;

    public DefaultCommandBus(IServiceProvider provider, ILogger<DefaultCommandBus> log)
    {
        _provider = provider;
        _log = log;
    }

    public async Task<TResult> SendAsync<TResult>(
        ICommand<TResult> command, ExecutionContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = await command.ValidateAsync(ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new CqrsValidationException("Command validation failed", validation.Failures);
        }

        var auth = await command.AuthorizeAsync(context, ct).ConfigureAwait(false);
        if (!auth.IsAllowed)
        {
            throw new CqrsAuthorizationException(
                string.Join("; ", auth.Errors.Select(e => $"{e.Code}: {e.Message}")), auth.Errors);
        }

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        var handler = _provider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No command handler registered for {command.GetType().FullName}");

        var method = handlerType.GetMethod("HandleAsync")!;
        _log.LogDebug("Dispatching {CommandType} via {Handler}", command.GetType().Name, handler.GetType().Name);
        var task = (Task<TResult>)method.Invoke(handler, new object[] { command, context, ct })!;
        return await task.ConfigureAwait(false);
    }
}

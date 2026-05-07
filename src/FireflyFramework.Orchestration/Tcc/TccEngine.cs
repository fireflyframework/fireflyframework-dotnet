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
using System.Reflection;
using FireflyFramework.Orchestration.Core;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Orchestration.Tcc;

/// <summary>
/// Try-Confirm-Cancel coordinator. Mirrors Java <c>TccEngine</c>: invokes <see cref="TryMethodAttribute"/>
/// methods on every <see cref="TccParticipantAttribute"/>; if all succeed, runs Confirm;
/// otherwise runs Cancel for the participants that completed Try.
/// </summary>
public sealed class TccEngine
{
    private readonly ILogger<TccEngine> _log;

    public TccEngine(ILogger<TccEngine> log) => _log = log;

    public async Task<TccResult> ExecuteAsync(object coordinator, IReadOnlyList<object> participants, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(participants);
        var coordinatorAttr = coordinator.GetType().GetCustomAttribute<TccAttribute>()
            ?? throw new InvalidOperationException($"{coordinator.GetType().Name} is not annotated with [Tcc]");

        var ctx = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Tcc, TccPhase = TccPhase.Try };
        var sw = Stopwatch.StartNew();
        var tryResults = new Dictionary<object, object?>();
        var triedParticipants = new List<object>();

        ctx.Status = ExecutionStatus.Trying;

        // Phase 1: Try
        foreach (var participant in participants)
        {
            var tryMethod = FindMethod(participant, typeof(TryMethodAttribute));
            if (tryMethod is null) continue;

            try
            {
                var result = await InvokeAsync(tryMethod, participant, ct).ConfigureAwait(false);
                tryResults[participant] = result;
                triedParticipants.Add(participant);
                _log.LogDebug("TCC {Tcc} Try succeeded on {Participant}", coordinatorAttr.Name, participant.GetType().Name);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "TCC {Tcc} Try failed on {Participant}; cancelling", coordinatorAttr.Name, participant.GetType().Name);
                await CancelAsync(coordinatorAttr, ctx, triedParticipants, tryResults, ct).ConfigureAwait(false);
                ctx.Status = ExecutionStatus.Canceled;
                ctx.TccPhase = TccPhase.Cancel;
                ctx.CompletedAt = DateTimeOffset.UtcNow;
                return new TccResult(false, ctx, ex, sw.Elapsed);
            }
        }

        // Phase 2: Confirm
        ctx.TccPhase = TccPhase.Confirm;
        ctx.Status = ExecutionStatus.Confirming;
        try
        {
            foreach (var participant in triedParticipants)
            {
                var confirmMethod = FindMethod(participant, typeof(ConfirmMethodAttribute));
                if (confirmMethod is null) continue;
                await InvokeAsync(confirmMethod, participant, ct, tryResults[participant]).ConfigureAwait(false);
            }

            ctx.Status = ExecutionStatus.Confirmed;
            ctx.CompletedAt = DateTimeOffset.UtcNow;
            return new TccResult(true, ctx, null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            // Confirm failures are catastrophic — log loudly and fail
            _log.LogError(ex, "TCC {Tcc} Confirm failed; manual intervention required", coordinatorAttr.Name);
            ctx.Status = ExecutionStatus.Failed;
            ctx.CompletedAt = DateTimeOffset.UtcNow;
            return new TccResult(false, ctx, ex, sw.Elapsed);
        }
    }

    private async Task CancelAsync(TccAttribute coordinatorAttr, OrchestrationExecutionContext ctx,
        List<object> triedParticipants, Dictionary<object, object?> tryResults, CancellationToken ct)
    {
        ctx.TccPhase = TccPhase.Cancel;
        ctx.Status = ExecutionStatus.Cancelling;
        foreach (var participant in triedParticipants)
        {
            var cancelMethod = FindMethod(participant, typeof(CancelMethodAttribute));
            if (cancelMethod is null) continue;
            try
            {
                await InvokeAsync(cancelMethod, participant, ct, tryResults[participant]).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "TCC {Tcc} Cancel failed on {Participant}", coordinatorAttr.Name, participant.GetType().Name);
            }
        }
    }

    private static MethodInfo? FindMethod(object instance, Type attributeType) =>
        instance.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttributes(attributeType, false).Length != 0);

    private static async Task<object?> InvokeAsync(MethodInfo method, object instance, CancellationToken ct, object? tryResult = null)
    {
        var args = method.GetParameters()
            .Select<ParameterInfo, object?>(p =>
            {
                if (p.GetCustomAttribute<FromTryAttribute>() is not null) return tryResult;
                if (p.ParameterType == typeof(CancellationToken)) return ct;
                return null;
            })
            .ToArray();

        var result = method.Invoke(instance, args);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return result;
    }
}

public sealed record TccResult(bool Success, OrchestrationExecutionContext Context, Exception? Error, TimeSpan Duration);

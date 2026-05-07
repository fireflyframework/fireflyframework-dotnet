// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Security.Core;

namespace FireflyFramework.Security.Authorization;

/// <summary>
/// Evaluates SpEL-style authorization expressions ("hasRole('ADMIN') and isAuthenticated()").
/// Mirrors Spring <c>SecurityExpressionHandler</c>.
/// </summary>
public interface IAuthorizationEvaluator
{
    Task<bool> EvaluateAsync(string expression, SecurityContext context, IReadOnlyDictionary<string, object?>? variables = null, CancellationToken ct = default);
}

/// <summary>
/// Token-based evaluator covering the predicates that show up in
/// 95% of <c>@PreAuthorize</c> usage. Combine with <c>and</c> / <c>or</c>;
/// negate with leading <c>!</c>. Variables substitute into a single argument
/// using the form <c>#name</c>.
/// </summary>
public sealed class DefaultAuthorizationEvaluator : IAuthorizationEvaluator
{
    public Task<bool> EvaluateAsync(string expression, SecurityContext context, IReadOnlyDictionary<string, object?>? variables = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(expression)) return Task.FromResult(true);
        var result = EvaluateOr(expression.Trim(), context, variables ?? new Dictionary<string, object?>());
        return Task.FromResult(result);
    }

    private static bool EvaluateOr(string expr, SecurityContext ctx, IReadOnlyDictionary<string, object?> vars)
    {
        var parts = SplitTopLevel(expr, " or ");
        return parts.Any(p => EvaluateAnd(p, ctx, vars));
    }

    private static bool EvaluateAnd(string expr, SecurityContext ctx, IReadOnlyDictionary<string, object?> vars)
    {
        var parts = SplitTopLevel(expr, " and ");
        return parts.All(p => EvaluatePrimary(p.Trim(), ctx, vars));
    }

    private static bool EvaluatePrimary(string expr, SecurityContext ctx, IReadOnlyDictionary<string, object?> vars)
    {
        var negate = expr.StartsWith("!", StringComparison.Ordinal);
        if (negate) expr = expr[1..].Trim();

        bool result;
        if (expr.StartsWith("(", StringComparison.Ordinal) && expr.EndsWith(")", StringComparison.Ordinal))
            result = EvaluateOr(expr[1..^1].Trim(), ctx, vars);
        else if (expr.StartsWith("hasRole(", StringComparison.OrdinalIgnoreCase))
            result = ctx.HasRole(StripQuotes(SubstituteVar(expr["hasRole(".Length..^1], vars)));
        else if (expr.StartsWith("hasAnyRole(", StringComparison.OrdinalIgnoreCase))
            result = expr["hasAnyRole(".Length..^1].Split(',').Select(s => StripQuotes(SubstituteVar(s, vars))).Any(ctx.HasRole);
        else if (expr.StartsWith("hasAuthority(", StringComparison.OrdinalIgnoreCase))
            result = ctx.HasAuthority(StripQuotes(SubstituteVar(expr["hasAuthority(".Length..^1], vars)));
        else if (expr.StartsWith("hasAnyAuthority(", StringComparison.OrdinalIgnoreCase))
            result = expr["hasAnyAuthority(".Length..^1].Split(',').Select(s => StripQuotes(SubstituteVar(s, vars))).Any(ctx.HasAuthority);
        else if (expr.Equals("isAuthenticated()", StringComparison.OrdinalIgnoreCase))
            result = ctx.IsAuthenticated;
        else if (expr.Equals("isAnonymous()", StringComparison.OrdinalIgnoreCase))
            result = !ctx.IsAuthenticated;
        else if (expr.Equals("permitAll()", StringComparison.OrdinalIgnoreCase))
            result = true;
        else if (expr.Equals("denyAll()", StringComparison.OrdinalIgnoreCase))
            result = false;
        else
            result = false;

        return negate ? !result : result;
    }

    private static IEnumerable<string> SplitTopLevel(string input, string separator)
    {
        var depth = 0;
        var start = 0;
        for (int i = 0; i <= input.Length - separator.Length; i++)
        {
            if (input[i] == '(') depth++;
            else if (input[i] == ')') depth--;
            else if (depth == 0 && string.CompareOrdinal(input, i, separator, 0, separator.Length) == 0)
            {
                yield return input[start..i];
                start = i + separator.Length;
                i = start - 1;
            }
        }
        yield return input[start..];
    }

    private static string SubstituteVar(string token, IReadOnlyDictionary<string, object?> vars)
    {
        var t = token.Trim();
        if (!t.StartsWith("#", StringComparison.Ordinal)) return t;
        var name = t[1..];
        return vars.TryGetValue(name, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
    }

    private static string StripQuotes(string s) => s.Trim().Trim('\'', '"');
}

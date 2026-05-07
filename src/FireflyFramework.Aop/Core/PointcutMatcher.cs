// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Text.RegularExpressions;

namespace FireflyFramework.Aop.Core;

/// <summary>
/// AspectJ-style pointcut matcher (subset). Supports <c>execution(ReturnType TypePattern.MethodPattern(..))</c>,
/// <c>within(TypePattern)</c>, and <c>@annotation(AttributeName)</c>.
/// </summary>
public static class PointcutMatcher
{
    public static bool Matches(string pointcut, JoinPoint jp)
    {
        if (string.IsNullOrWhiteSpace(pointcut)) return false;
        var trimmed = pointcut.Trim();

        if (trimmed.StartsWith("execution(", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal))
            return MatchesExecution(trimmed[10..^1], jp);
        if (trimmed.StartsWith("within(", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal))
            return MatchesWithin(trimmed[7..^1], jp);
        if (trimmed.StartsWith("@annotation(", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal))
            return MatchesAnnotation(trimmed[12..^1], jp);

        return false;
    }

    private static bool MatchesExecution(string body, JoinPoint jp)
    {
        var spaceIdx = body.IndexOf(' ');
        if (spaceIdx < 0) return false;
        var rest = body[(spaceIdx + 1)..].TrimStart();
        var parenIdx = rest.IndexOf('(');
        if (parenIdx < 0) return false;
        var typeAndMethod = rest[..parenIdx].TrimEnd();
        var dot = typeAndMethod.LastIndexOf('.');
        if (dot < 0) return false;
        var typePattern = typeAndMethod[..dot].Trim();
        var methodPattern = typeAndMethod[(dot + 1)..].Trim();
        return MatchPattern(typePattern, jp.TypeName) && MatchPattern(methodPattern, jp.MethodName);
    }

    private static bool MatchesWithin(string body, JoinPoint jp) => MatchPattern(body, jp.TypeName);

    private static bool MatchesAnnotation(string attributeName, JoinPoint jp) =>
        jp.Method.GetCustomAttributes(true).Any(a =>
            string.Equals(a.GetType().Name, attributeName, StringComparison.Ordinal) ||
            string.Equals(a.GetType().Name, attributeName + "Attribute", StringComparison.Ordinal));

    private static bool MatchPattern(string pattern, string value)
    {
        if (pattern == "*" || pattern == "*.*") return true;
        var rx = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\.\\.", ".*") + "$";
        return Regex.IsMatch(value, rx);
    }
}

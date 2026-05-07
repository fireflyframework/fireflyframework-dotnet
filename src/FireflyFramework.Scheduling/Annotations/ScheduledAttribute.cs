// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Scheduling.Annotations;

/// <summary>
/// Declares a method as a scheduled task. Mirrors Spring <c>@Scheduled</c> /
/// pyfly <c>@scheduled</c>. Exactly one of <see cref="Cron"/>, <see cref="FixedRate"/>,
/// or <see cref="FixedDelay"/> must be set.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ScheduledAttribute : Attribute
{
    public string? Cron { get; init; }
    public string? Zone { get; init; }
    public TimeSpan FixedRate { get; init; }
    public TimeSpan FixedDelay { get; init; }
    public TimeSpan InitialDelay { get; init; }
}

/// <summary>Marks a method as eligible for asynchronous execution via <c>TaskExecutor</c>.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AsyncAttribute : Attribute
{
    public string? Executor { get; init; }
}

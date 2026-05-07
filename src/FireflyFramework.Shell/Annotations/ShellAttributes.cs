// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Shell.Annotations;

/// <summary>Marks a class as a shell command source. Mirrors Spring <c>@ShellComponent</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ShellComponentAttribute : Attribute
{
    public string? Group { get; init; }
}

/// <summary>Declares a shell command. The method name (or <see cref="Name"/>) is the verb.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ShellMethodAttribute : Attribute
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

/// <summary>Positional argument metadata.</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ShellArgumentAttribute : Attribute
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

/// <summary>Named option (--option-name) metadata.</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ShellOptionAttribute : Attribute
{
    public string? Long { get; init; }
    public char? Short { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
}

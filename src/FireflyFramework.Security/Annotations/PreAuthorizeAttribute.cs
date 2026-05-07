// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Security.Annotations;

/// <summary>
/// Declarative authorization rule applied before the method runs.
/// Mirrors Spring <c>@PreAuthorize</c> / pyfly <c>@pre_authorize</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class PreAuthorizeAttribute : Attribute
{
    public PreAuthorizeAttribute(string expression) { Expression = expression; }
    public string Expression { get; }
}

/// <summary>Authorization rule applied after the method runs (filtered result).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class PostAuthorizeAttribute : Attribute
{
    public PostAuthorizeAttribute(string expression) { Expression = expression; }
    public string Expression { get; }
}

/// <summary>Marks a method as requiring authentication only (no fine-grained rule).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class SecuredAttribute : Attribute
{
    public SecuredAttribute(params string[] roles) { Roles = roles; }
    public string[] Roles { get; }
}

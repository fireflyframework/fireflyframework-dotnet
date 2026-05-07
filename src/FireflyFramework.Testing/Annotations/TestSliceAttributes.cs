// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Testing.Annotations;

/// <summary>Mark a test class as exercising the data slice (R2DBC + repos). Mirrors Spring <c>@DataJpaTest</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DataTestAttribute : Attribute { }

/// <summary>Mark a test class as exercising the service slice (handlers, sagas, no web). Mirrors Spring <c>@SpringBootTest</c> minus web layer.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ServiceTestAttribute : Attribute { }

/// <summary>Mark a test class as exercising the web slice (TestServer, controllers, filters).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class WebTestAttribute : Attribute { }

/// <summary>Replace a service registration with a mock (test setup helper).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class MockBeanAttribute : Attribute { }

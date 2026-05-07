// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Admin.Configuration;

public sealed class FireflyAdminServerOptions
{
    public const string SectionName = "Firefly:Admin:Server";

    public string BasePath { get; set; } = "/admin";
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public bool RequireAuthorization { get; set; } = true;
}

public sealed class FireflyAdminClientOptions
{
    public const string SectionName = "Firefly:Admin:Client";

    public string ServerUrl { get; set; } = "http://localhost:5000/admin";
    public string Name { get; set; } = "firefly-app";
    public string? ManagementUrl { get; set; }
    public string? HealthUrl { get; set; }
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
    public bool AutoRegister { get; set; } = true;
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

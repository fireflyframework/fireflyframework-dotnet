// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Session.Configuration;

public sealed class FireflySessionOptions
{
    public const string SectionName = "Firefly:Session";

    public string Provider { get; set; } = "Memory"; // Memory | Redis
    public string CookieName { get; set; } = "FIREFLY_SESSION";
    public bool SecureCookie { get; set; } = true;
    public TimeSpan MaxInactiveInterval { get; set; } = TimeSpan.FromMinutes(30);
    public RedisSessionOptions Redis { get; set; } = new();
}

public sealed class RedisSessionOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string KeyPrefix { get; set; } = "firefly:session:";
}

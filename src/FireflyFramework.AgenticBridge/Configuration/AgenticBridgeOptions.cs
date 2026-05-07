// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.AgenticBridge.Configuration;

public sealed class FireflyAgenticBridgeOptions
{
    public const string SectionName = "Firefly:Agentic:Bridge";

    public string Transport { get; set; } = "Rest"; // Rest | Sse | WebSocket | Queue
    public string BaseUrl { get; set; } = "http://localhost:7000";
    public string? ApiKey { get; set; }
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxAttempts { get; set; } = 3;
}

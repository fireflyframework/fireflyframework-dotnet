// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Actuator.Configuration;

public sealed class FireflyActuatorOptions
{
    public const string SectionName = "Firefly:Actuator";

    public string BasePath { get; set; } = "/actuator";
    public List<string> ExposeEndpoints { get; set; } = new() { "info", "metrics", "env", "beans", "loggers", "threaddump", "mappings" };
    public bool RequireAuthorization { get; set; } = true;
}

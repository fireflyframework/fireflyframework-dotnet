// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.Core;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Actuator.Endpoints;

public sealed class LoggersEndpoint : IActuatorEndpoint
{
    public string Id => "loggers";

    public Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct)
    {
        var levels = Enum.GetNames<LogLevel>();
        return Task.FromResult<object?>(new
        {
            levels,
            loggers = new Dictionary<string, object>
            {
                ["ROOT"] = new { configuredLevel = LogLevel.Information.ToString(), effectiveLevel = LogLevel.Information.ToString() },
            },
        });
    }
}

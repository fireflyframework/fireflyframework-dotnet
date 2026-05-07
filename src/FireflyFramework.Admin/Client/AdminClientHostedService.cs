// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Net.Http.Json;
using FireflyFramework.Admin.Configuration;
using FireflyFramework.Admin.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Admin.Client;

/// <summary>Self-registers the running app with a Firefly Admin Server and beats periodically.</summary>
public sealed class AdminClientHostedService : BackgroundService
{
    private readonly IHttpClientFactory _factory;
    private readonly IOptionsMonitor<FireflyAdminClientOptions> _options;
    private readonly ILogger<AdminClientHostedService> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public AdminClientHostedService(IHttpClientFactory factory, IOptionsMonitor<FireflyAdminClientOptions> options, ILogger<AdminClientHostedService> logger)
    {
        _factory = factory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = _options.CurrentValue;
        if (!opt.AutoRegister) return;

        var http = _factory.CreateClient("firefly-admin");
        var instance = new AdminInstance(
            Id: _instanceId,
            Name: opt.Name,
            ManagementUrl: opt.ManagementUrl ?? string.Empty,
            HealthUrl: opt.HealthUrl ?? string.Empty,
            RegisteredAt: DateTimeOffset.UtcNow,
            LastHeartbeat: DateTimeOffset.UtcNow,
            Status: "UP",
            Metadata: opt.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value));

        try
        {
            await http.PostAsJsonAsync($"{opt.ServerUrl.TrimEnd('/')}/instances", instance, stoppingToken).ConfigureAwait(false);
            _logger.LogInformation("Registered with admin server {Url} as {Id}", opt.ServerUrl, _instanceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initial admin registration failed; will keep beating");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await http.PutAsync($"{opt.ServerUrl.TrimEnd('/')}/instances/{_instanceId}/heartbeat?status=UP", null, stoppingToken).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogDebug(ex, "Admin heartbeat failed"); }

            try { await Task.Delay(opt.HeartbeatInterval, stoppingToken).ConfigureAwait(false); }
            catch (TaskCanceledException) { break; }
        }

        try { await http.DeleteAsync($"{opt.ServerUrl.TrimEnd('/')}/instances/{_instanceId}", CancellationToken.None).ConfigureAwait(false); }
        catch { /* ignore on shutdown */ }
    }
}

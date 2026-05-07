// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Reflection;
using FireflyFramework.Shell.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Shell.Core;

/// <summary>
/// Reflective dispatcher: scans <see cref="ShellComponentAttribute"/> classes
/// registered in DI and routes each command verb to the matching method.
/// </summary>
public sealed class DefaultShellRunner : IShellRunner
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<DefaultShellRunner> _logger;

    public DefaultShellRunner(IServiceProvider provider, ILogger<DefaultShellRunner> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task<int> RunOnceAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        if (args.Count == 0) { Console.WriteLine("Usage: <verb> [args...]"); return 1; }
        var verb = args[0];
        var commands = DiscoverCommands();
        if (!commands.TryGetValue(verb, out var entry))
        {
            Console.Error.WriteLine($"Unknown command: {verb}");
            Console.Error.WriteLine($"Available: {string.Join(", ", commands.Keys.OrderBy(k => k))}");
            return 1;
        }

        try
        {
            var bound = BindParameters(entry.method, args.Skip(1).ToArray());
            var result = entry.method.Invoke(entry.target, bound);
            if (result is Task t) await t.ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shell command {Verb} failed", verb);
            return 1;
        }
    }

    public async Task RunInteractiveAsync(CancellationToken ct)
    {
        Console.WriteLine("firefly-shell (type 'help' for commands, 'exit' to quit)");
        while (!ct.IsCancellationRequested)
        {
            Console.Write("firefly> ");
            var line = Console.ReadLine();
            if (line is null || line.Trim() is "exit" or "quit") return;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            await RunOnceAsync(parts, ct).ConfigureAwait(false);
        }
    }

    private Dictionary<string, (object target, MethodInfo method)> DiscoverCommands()
    {
        var commands = new Dictionary<string, (object, MethodInfo)>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in _provider.GetServices<IFireflyShellComponent>())
        {
            var t = component.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = m.GetCustomAttribute<ShellMethodAttribute>();
                if (attr is null) continue;
                var verb = attr.Name ?? KebabCase(m.Name);
                commands[verb] = (component, m);
            }
        }
        return commands;
    }

    private static object?[] BindParameters(MethodInfo method, IReadOnlyList<string> args)
    {
        var ps = method.GetParameters();
        var result = new object?[ps.Length];
        var positional = args.Where(a => !a.StartsWith("-", StringComparison.Ordinal)).ToList();
        var posIdx = 0;
        for (int i = 0; i < ps.Length; i++)
        {
            var p = ps[i];
            var optAttr = p.GetCustomAttribute<ShellOptionAttribute>();
            if (optAttr is not null)
            {
                var longName = optAttr.Long ?? KebabCase(p.Name!);
                var token = args.FirstOrDefault(a => a.StartsWith($"--{longName}=", StringComparison.Ordinal));
                if (token is not null)
                {
                    var raw = token[$"--{longName}=".Length..];
                    result[i] = ConvertTo(raw, p.ParameterType);
                }
                else if (optAttr.DefaultValue is not null)
                    result[i] = ConvertTo(optAttr.DefaultValue, p.ParameterType);
                else if (p.HasDefaultValue)
                    result[i] = p.DefaultValue;
                else if (optAttr.Required)
                    throw new ArgumentException($"Missing required option --{longName}");
            }
            else
            {
                if (posIdx < positional.Count) result[i] = ConvertTo(positional[posIdx++], p.ParameterType);
                else if (p.HasDefaultValue) result[i] = p.DefaultValue;
                else if (p.ParameterType == typeof(CancellationToken)) result[i] = CancellationToken.None;
            }
        }
        return result;
    }

    private static object? ConvertTo(string raw, Type type) =>
        type == typeof(string) ? raw :
        type == typeof(int) ? int.Parse(raw) :
        type == typeof(long) ? long.Parse(raw) :
        type == typeof(bool) ? bool.Parse(raw) :
        type == typeof(double) ? double.Parse(raw) :
        type.IsEnum ? Enum.Parse(type, raw, ignoreCase: true) :
        Convert.ChangeType(raw, type);

    private static string KebabCase(string s)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i])) sb.Append('-');
            sb.Append(char.ToLowerInvariant(s[i]));
        }
        return sb.ToString();
    }
}

public interface IFireflyShellComponent { }

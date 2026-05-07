// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Shell.Core;

public sealed class ApplicationArguments : IApplicationArguments
{
    public ApplicationArguments(IEnumerable<string> args)
    {
        SourceArgs = args.ToList();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var nonOptions = new List<string>();
        for (int i = 0; i < SourceArgs.Count; i++)
        {
            var token = SourceArgs[i];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var rest = token[2..];
                var eq = rest.IndexOf('=');
                if (eq >= 0) options[rest[..eq]] = rest[(eq + 1)..];
                else options[rest] = null; // bare --flag — value (if any) must use --flag=value form
            }
            else nonOptions.Add(token);
        }
        NonOptionArgs = nonOptions;
        OptionArgs = options;
    }

    public IReadOnlyList<string> SourceArgs { get; }
    public IReadOnlyList<string> NonOptionArgs { get; }
    public IReadOnlyDictionary<string, string?> OptionArgs { get; }
    public bool ContainsOption(string name) => OptionArgs.ContainsKey(name);
}

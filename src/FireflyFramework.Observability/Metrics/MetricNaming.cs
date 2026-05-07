// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text.RegularExpressions;

namespace FireflyFramework.Observability.Metrics;

/// <summary>Utility for naming metrics consistently. Mirrors Java <c>MetricNaming</c>.</summary>
public static partial class MetricNaming
{
    [GeneratedRegex(@"^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleRegex();

    public static string Prefix(string module)
    {
        if (string.IsNullOrWhiteSpace(module) || !ModuleRegex().IsMatch(module))
        {
            throw new ArgumentException("Module names must be lowercase, alphanumeric and underscore-only.", nameof(module));
        }

        return $"firefly.{module}";
    }

    public static string Name(string prefix, string metric) => $"{prefix}.{metric}";
}

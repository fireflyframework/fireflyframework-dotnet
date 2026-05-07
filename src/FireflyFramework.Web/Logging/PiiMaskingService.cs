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

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Web.Logging;

/// <summary>
/// Masks PII in log lines and JSON payloads. Mirrors Java <c>PiiMaskingService</c>.
/// </summary>
public sealed class PiiMaskingService
{
    private readonly PiiMaskingOptions _options;
    private readonly Regex[] _patterns;
    private readonly HashSet<string> _fields;

    public PiiMaskingService(IOptions<PiiMaskingOptions> options)
    {
        _options = options.Value;
        _patterns = _options.SensitivePatterns
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();
        _fields = new HashSet<string>(_options.SensitiveFields, StringComparer.OrdinalIgnoreCase);
    }

    public string MaskString(string input)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(input))
        {
            return input;
        }

        var output = input;
        foreach (var pattern in _patterns)
        {
            output = pattern.Replace(output, m => MaskValue(m.Value));
        }

        return output;
    }

    public JsonElement MaskJson(JsonElement element)
    {
        if (!_options.Enabled)
        {
            return element;
        }

        using var doc = JsonDocument.Parse(MaskNode(element).GetRawText());
        return doc.RootElement.Clone();
    }

    private JsonElement MaskNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (var prop in element.EnumerateObject())
                        {
                            writer.WritePropertyName(prop.Name);
                            if (_fields.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.String)
                            {
                                writer.WriteStringValue(MaskValue(prop.Value.GetString() ?? string.Empty));
                            }
                            else
                            {
                                MaskNode(prop.Value).WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();
                    }

                    var ms = JsonDocument.Parse(stream.ToArray()).RootElement;
                    return ms.Clone();
                }

            case JsonValueKind.Array:
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartArray();
                        foreach (var item in element.EnumerateArray())
                        {
                            MaskNode(item).WriteTo(writer);
                        }

                        writer.WriteEndArray();
                    }

                    var ms = JsonDocument.Parse(stream.ToArray()).RootElement;
                    return ms.Clone();
                }

            default:
                return element;
        }
    }

    public string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Keep a configurable prefix and suffix visible (e.g. "12**********34")
        // so a human troubleshooting a log can still recognise the value while
        // the body remains redacted. We clamp to the input length so a short
        // value with VisiblePrefix > value.Length doesn't throw.
        var keepStart = Math.Min(_options.VisiblePrefix, value.Length);
        var keepEnd = Math.Min(_options.VisibleSuffix, Math.Max(value.Length - keepStart, 0));
        var maskLength = Math.Max(value.Length - keepStart - keepEnd, 0);
        return value[..keepStart] + new string(_options.MaskCharacter[0], maskLength) + value[^keepEnd..];
    }
}

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

namespace FireflyFramework.Web.Logging;

/// <summary>Configuration for <see cref="PiiMaskingService"/>. Mirrors Java <c>PiiMaskingProperties</c>.</summary>
public sealed class PiiMaskingOptions
{
    public const string SectionName = "Firefly:Web:PiiMasking";

    public bool Enabled { get; set; } = true;

    public string MaskCharacter { get; set; } = "*";

    public int VisiblePrefix { get; set; } = 2;

    public int VisibleSuffix { get; set; } = 2;

    public List<string> SensitiveFields { get; set; } = new()
    {
        "password", "secret", "token", "apiKey", "authorization",
        "ssn", "creditCard", "cardNumber", "cvv", "iban", "pin",
    };

    public List<string> SensitivePatterns { get; set; } = new();
}

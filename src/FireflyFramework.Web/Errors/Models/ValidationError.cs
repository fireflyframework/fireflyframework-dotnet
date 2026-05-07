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

using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>Field-level validation failure. Mirrors Java <c>ErrorResponse.ValidationError</c>.</summary>
public sealed class ValidationError
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }

    public ValidationError() { }

    public ValidationError(string field, string code, string message, Dictionary<string, object?>? metadata = null)
    {
        Field = field;
        Code = code;
        Message = message;
        Metadata = metadata;
    }
}

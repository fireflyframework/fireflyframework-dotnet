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

namespace FireflyFramework.Web.Cors;

/// <summary>Centralised CORS configuration. Mirrors Java <c>CorsProperties</c>.</summary>
public sealed class FireflyCorsOptions
{
    public const string SectionName = "Firefly:Web:Cors";

    public bool Enabled { get; set; } = true;
    public List<string> AllowedOrigins { get; set; } = new() { "*" };
    public List<string> AllowedMethods { get; set; } = new() { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" };
    public List<string> AllowedHeaders { get; set; } = new() { "*" };
    public List<string> ExposedHeaders { get; set; } = new() { "X-Correlation-Id", "X-Request-Id", "X-Idempotency-Key" };
    public bool AllowCredentials { get; set; }
    public TimeSpan PreflightMaxAge { get; set; } = TimeSpan.FromMinutes(10);
}

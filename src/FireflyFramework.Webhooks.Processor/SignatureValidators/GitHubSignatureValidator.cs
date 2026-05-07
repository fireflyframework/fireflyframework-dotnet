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

using System.Security.Cryptography;
using System.Text;

namespace FireflyFramework.Webhooks.Processor.SignatureValidators;

/// <summary>
/// Validates GitHub webhook signatures (<c>X-Hub-Signature-256</c> header, value
/// <c>sha256=&lt;hex_hmac&gt;</c>). The HMAC-SHA256 is computed over the raw payload
/// using the webhook secret. Mirrors GitHub's documented algorithm.
/// </summary>
public sealed class GitHubSignatureValidator : IWebhookSignatureValidator
{
    public const string HeaderName = "X-Hub-Signature-256";
    private const string Prefix = "sha256=";

    public Task<bool> ValidateSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers, string secret, CancellationToken ct = default)
    {
        if (!headers.TryGetValue(HeaderName, out var raw) || !raw.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        var supplied = raw[Prefix.Length..].ToLowerInvariant();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied),
            Encoding.ASCII.GetBytes(expected)));
    }
}

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

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FireflyFramework.Webhooks.Processor.SignatureValidators;

/// <summary>
/// Validates a Stripe webhook signature header (<c>Stripe-Signature</c>) of the form
/// <c>t=&lt;timestamp&gt;,v1=&lt;hex_hmac&gt;</c>. The HMAC-SHA256 is computed over
/// <c>{timestamp}.{rawPayload}</c> with the endpoint secret. Mirrors the Java
/// <c>StripeSignatureValidator</c>.
/// </summary>
public sealed class StripeSignatureValidator : IWebhookSignatureValidator
{
    private readonly TimeSpan _tolerance;
    public const string HeaderName = "Stripe-Signature";

    public StripeSignatureValidator(TimeSpan? tolerance = null) =>
        _tolerance = tolerance ?? TimeSpan.FromMinutes(5);

    public Task<bool> ValidateSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers, string secret, CancellationToken ct = default)
    {
        if (!headers.TryGetValue(HeaderName, out var raw))
        {
            return Task.FromResult(false);
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            switch (kv[0])
            {
                case "t": timestamp = kv[1]; break;
                case "v1": signatures.Add(kv[1]); break;
            }
        }

        if (timestamp is null || signatures.Count == 0)
        {
            return Task.FromResult(false);
        }

        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts))
        {
            return Task.FromResult(false);
        }

        var elapsed = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(ts);
        if (elapsed.Duration() > _tolerance)
        {
            return Task.FromResult(false);
        }

        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();

        foreach (var candidate in signatures)
        {
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(candidate.ToLowerInvariant()),
                Encoding.ASCII.GetBytes(expected)))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }
}

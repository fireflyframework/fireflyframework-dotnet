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
/// Generic HMAC validator: read a header, parse out an optional prefix, compare
/// HMAC-{algo} of the raw payload using the secret. Useful for providers that ship a
/// plain HMAC without a timestamp tolerance window.
/// </summary>
public sealed class HmacSignatureValidator : IWebhookSignatureValidator
{
    private readonly string _headerName;
    private readonly HmacAlgorithm _algorithm;
    private readonly string? _prefix;

    public HmacSignatureValidator(string headerName, HmacAlgorithm algorithm = HmacAlgorithm.Sha256, string? prefix = null)
    {
        _headerName = headerName;
        _algorithm = algorithm;
        _prefix = prefix;
    }

    public Task<bool> ValidateSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers, string secret, CancellationToken ct = default)
    {
        if (!headers.TryGetValue(_headerName, out var raw))
        {
            return Task.FromResult(false);
        }

        if (_prefix is not null)
        {
            if (!raw.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(false);
            raw = raw[_prefix.Length..];
        }

        using HMAC hmac = _algorithm switch
        {
            HmacAlgorithm.Sha1 => new HMACSHA1(Encoding.UTF8.GetBytes(secret)),
            HmacAlgorithm.Sha512 => new HMACSHA512(Encoding.UTF8.GetBytes(secret)),
            _ => new HMACSHA256(Encoding.UTF8.GetBytes(secret)),
        };

        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var supplied = raw.ToLowerInvariant();
        return Task.FromResult(CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(supplied)));
    }
}

public enum HmacAlgorithm { Sha1, Sha256, Sha512 }

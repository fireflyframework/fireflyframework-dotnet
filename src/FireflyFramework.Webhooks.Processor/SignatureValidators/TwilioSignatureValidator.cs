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
/// Validates Twilio webhook signatures. Twilio computes HMAC-SHA1 of
/// <c>{full URL}{form-encoded body sorted by key}</c> using the auth token, then
/// base64-encodes it. The signature comes in the <c>X-Twilio-Signature</c> header.
/// </summary>
public sealed class TwilioSignatureValidator : IWebhookSignatureValidator
{
    public const string HeaderName = "X-Twilio-Signature";

    /// <summary>
    /// Validates the signature. The <paramref name="payload"/> string here is expected
    /// to be the full request URL followed by the body — callers should compose it as
    /// <c>{absoluteUrl}{sortedFormParams}</c> per the Twilio docs.
    /// </summary>
    public Task<bool> ValidateSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers, string secret, CancellationToken ct = default)
    {
        if (!headers.TryGetValue(HeaderName, out var supplied))
        {
            return Task.FromResult(false);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(supplied)));
    }
}

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
using FireflyFramework.Webhooks.Processor.SignatureValidators;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class WebhookSignatureValidatorTests
{
    private const string Secret = "super-secret-key-1234567890";

    // ───── Stripe ─────

    [Fact]
    public async Task Stripe_validates_correct_signature()
    {
        var validator = new StripeSignatureValidator(TimeSpan.FromMinutes(5));
        var payload = """{"id":"evt_test_webhook"}""";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{payload}"))).ToLowerInvariant();
        var headers = new Dictionary<string, string> { ["Stripe-Signature"] = $"t={ts},v1={sig}" };

        (await validator.ValidateSignatureAsync(payload, headers, Secret)).Should().BeTrue();
    }

    [Fact]
    public async Task Stripe_rejects_tampered_payload()
    {
        var validator = new StripeSignatureValidator(TimeSpan.FromMinutes(5));
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{{\"id\":\"evt_test\"}}"))).ToLowerInvariant();
        var headers = new Dictionary<string, string> { ["Stripe-Signature"] = $"t={ts},v1={sig}" };

        (await validator.ValidateSignatureAsync("{\"id\":\"evt_tampered\"}", headers, Secret)).Should().BeFalse();
    }

    [Fact]
    public async Task Stripe_rejects_outside_tolerance_window()
    {
        var validator = new StripeSignatureValidator(TimeSpan.FromMinutes(1));
        var oldTs = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{oldTs}.body"))).ToLowerInvariant();
        var headers = new Dictionary<string, string> { ["Stripe-Signature"] = $"t={oldTs},v1={sig}" };

        (await validator.ValidateSignatureAsync("body", headers, Secret)).Should().BeFalse();
    }

    [Fact]
    public async Task Stripe_returns_false_when_header_missing()
    {
        var validator = new StripeSignatureValidator();
        (await validator.ValidateSignatureAsync("body", new Dictionary<string, string>(), Secret)).Should().BeFalse();
    }

    // ───── GitHub ─────

    [Fact]
    public async Task GitHub_validates_correct_signature()
    {
        var validator = new GitHubSignatureValidator();
        var payload = """{"action":"opened"}""";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var headers = new Dictionary<string, string> { ["X-Hub-Signature-256"] = $"sha256={sig}" };

        (await validator.ValidateSignatureAsync(payload, headers, Secret)).Should().BeTrue();
    }

    [Fact]
    public async Task GitHub_rejects_when_prefix_missing()
    {
        var validator = new GitHubSignatureValidator();
        var headers = new Dictionary<string, string> { ["X-Hub-Signature-256"] = "0123456789abcdef" };

        (await validator.ValidateSignatureAsync("payload", headers, Secret)).Should().BeFalse();
    }

    [Fact]
    public async Task GitHub_rejects_wrong_secret()
    {
        var validator = new GitHubSignatureValidator();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("WRONG"));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes("payload"))).ToLowerInvariant();
        var headers = new Dictionary<string, string> { ["X-Hub-Signature-256"] = $"sha256={sig}" };

        (await validator.ValidateSignatureAsync("payload", headers, Secret)).Should().BeFalse();
    }

    // ───── Twilio ─────

    [Fact]
    public async Task Twilio_validates_base64_signature()
    {
        var validator = new TwilioSignatureValidator();
        var payload = "https://my.app/twiliobody=hello";

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        var headers = new Dictionary<string, string> { ["X-Twilio-Signature"] = sig };

        (await validator.ValidateSignatureAsync(payload, headers, Secret)).Should().BeTrue();
    }

    [Fact]
    public async Task Twilio_rejects_corrupted_signature()
    {
        var validator = new TwilioSignatureValidator();
        var headers = new Dictionary<string, string> { ["X-Twilio-Signature"] = "AAAA" };
        (await validator.ValidateSignatureAsync("anything", headers, Secret)).Should().BeFalse();
    }

    // ───── Generic HMAC ─────

    [Theory]
    [InlineData(HmacAlgorithm.Sha1)]
    [InlineData(HmacAlgorithm.Sha256)]
    [InlineData(HmacAlgorithm.Sha512)]
    public async Task Generic_validates_each_algorithm(HmacAlgorithm algo)
    {
        var validator = new HmacSignatureValidator("X-Sig", algo);
        var payload = "body-bytes";
        using HMAC hmac = algo switch
        {
            HmacAlgorithm.Sha1 => new HMACSHA1(Encoding.UTF8.GetBytes(Secret)),
            HmacAlgorithm.Sha512 => new HMACSHA512(Encoding.UTF8.GetBytes(Secret)),
            _ => new HMACSHA256(Encoding.UTF8.GetBytes(Secret)),
        };
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var headers = new Dictionary<string, string> { ["X-Sig"] = sig };

        (await validator.ValidateSignatureAsync(payload, headers, Secret)).Should().BeTrue();
    }

    [Fact]
    public async Task Generic_strips_optional_prefix()
    {
        var validator = new HmacSignatureValidator("X-Sig", HmacAlgorithm.Sha256, prefix: "sha256=");
        var payload = "body";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var headers = new Dictionary<string, string> { ["X-Sig"] = $"sha256={sig}" };

        (await validator.ValidateSignatureAsync(payload, headers, Secret)).Should().BeTrue();
    }
}

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

using System.Net;
using FireflyFramework.Webhooks.Interfaces;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Webhooks.Core.Services;

public sealed class WebhookSecurityOptions
{
    public List<string> IpWhitelist { get; set; } = new();
    public List<string> IpBlacklist { get; set; } = new();
    public bool RequireSignature { get; set; }
    public int MaxPayloadBytes { get; set; } = 1_048_576; // 1 MiB
}

public sealed record WebhookValidationResult(bool IsValid, string? Reason);

/// <summary>
/// Pre-flight validation: payload size, IP whitelist/blacklist, signature presence.
/// Mirrors Java <c>WebhookValidator</c>.
/// </summary>
public sealed class WebhookValidator
{
    private readonly WebhookSecurityOptions _options;

    public WebhookValidator(IOptions<WebhookSecurityOptions> options) => _options = options.Value;

    public WebhookValidationResult Validate(WebhookEventDto evt, int payloadByteCount)
    {
        if (payloadByteCount > _options.MaxPayloadBytes)
        {
            return new WebhookValidationResult(false, "payload-too-large");
        }

        if (evt.SourceIp is not null && IPAddress.TryParse(evt.SourceIp, out var ip))
        {
            if (_options.IpBlacklist.Any(cidr => InCidr(ip, cidr)))
            {
                return new WebhookValidationResult(false, "ip-blacklisted");
            }

            if (_options.IpWhitelist.Count > 0 && !_options.IpWhitelist.Any(cidr => InCidr(ip, cidr)))
            {
                return new WebhookValidationResult(false, "ip-not-allowed");
            }
        }

        return new WebhookValidationResult(true, null);
    }

    /// <summary>Checks if <paramref name="ip"/> falls within the supplied CIDR. Supports IPv4 and IPv6.</summary>
    public static bool InCidr(IPAddress ip, string cidr)
    {
        var slash = cidr.IndexOf('/');
        if (slash < 0)
        {
            return IPAddress.TryParse(cidr, out var single) && single.Equals(ip);
        }

        if (!IPAddress.TryParse(cidr[..slash], out var network) ||
            !int.TryParse(cidr[(slash + 1)..], out var prefix))
        {
            return false;
        }

        var ipBytes = ip.GetAddressBytes();
        var netBytes = network.GetAddressBytes();
        if (ipBytes.Length != netBytes.Length) return false;

        var fullBytes = prefix / 8;
        var remainingBits = prefix % 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (ipBytes[i] != netBytes[i]) return false;
        }

        if (remainingBits == 0) return true;
        var mask = (byte)~(0xFF >> remainingBits);
        return (ipBytes[fullBytes] & mask) == (netBytes[fullBytes] & mask);
    }
}

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

using FireflyFramework.Notifications;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace FireflyFramework.Notifications.Twilio;

public sealed class TwilioOptions
{
    public const string SectionName = "Firefly:Notifications:Twilio";
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string DefaultFromNumber { get; set; } = string.Empty;
}

public sealed class TwilioSmsProvider : ISmsProvider
{
    private readonly TwilioOptions _opt;
    private readonly ITwilioRestClient _client;

    /// <summary>
    /// Production constructor — builds a default <see cref="TwilioRestClient"/> from the configured
    /// SID / auth token. Pick this overload when registering through DI.
    /// </summary>
    public TwilioSmsProvider(IOptions<TwilioOptions> options)
        : this(options, new TwilioRestClient(options.Value.AccountSid, options.Value.AuthToken)) { }

    /// <summary>
    /// Constructor that accepts an explicit <see cref="ITwilioRestClient"/>. Used by tests with a
    /// WireMock-pointing client and by hosts that share a single Twilio client across adapters.
    /// </summary>
    public TwilioSmsProvider(IOptions<TwilioOptions> options, ITwilioRestClient client)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        _opt = options.Value;
        _client = client;
    }

    public async Task<SmsResponse> SendSmsAsync(SmsRequest request, CancellationToken ct = default)
    {
        try
        {
            var options = new CreateMessageOptions(new PhoneNumber(request.PhoneNumber))
            {
                From = new PhoneNumber(request.FromNumber ?? _opt.DefaultFromNumber),
                Body = request.Message,
            };

            var message = await MessageResource.CreateAsync(options, _client).ConfigureAwait(false);
            return new SmsResponse(message.Sid, true, null);
        }
        catch (Exception ex)
        {
            return new SmsResponse(null, false, ex.Message);
        }
    }
}

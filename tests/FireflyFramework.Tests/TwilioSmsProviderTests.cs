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
using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Twilio;
using Microsoft.Extensions.Options;
using NSubstitute;
using Twilio.Clients;
using Twilio.Http;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// NSubstitute-mocked tests for <see cref="TwilioSmsProvider"/>. Verify the request shape
/// (to / from / body) and the success / failure response handling, without touching the real
/// Twilio API.
///
/// The Twilio SDK ultimately calls <see cref="ITwilioRestClient.RequestAsync"/> with a
/// <see cref="Request"/>. Asserting against that interface lets us pin exactly the wire-shape
/// the SDK builds for <c>POST /2010-04-01/Accounts/{sid}/Messages.json</c>.
/// </summary>
public sealed class TwilioSmsProviderTests
{
    private static TwilioSmsProvider NewProvider(ITwilioRestClient client) => new(
        Options.Create(new TwilioOptions
        {
            AccountSid = "AC0000000000000000000000000000",
            AuthToken = "auth",
            DefaultFromNumber = "+15551234567",
        }),
        client);

    [Fact]
    public async Task SendSmsAsync_HappyPath_PostsTo_MessagesEndpoint_AndReturnsSid()
    {
        var client = Substitute.For<ITwilioRestClient>();
        client.AccountSid.Returns("AC0000000000000000000000000000");
        Request? captured = null;
        client.RequestAsync(Arg.Do<Request>(r => captured = r))
            .Returns(new Response(HttpStatusCode.Created, """
                {
                    "sid": "SM123",
                    "account_sid": "AC0000000000000000000000000000",
                    "to": "+15557654321",
                    "from": "+15551234567",
                    "body": "ping",
                    "status": "queued"
                }
                """));

        var resp = await NewProvider(client).SendSmsAsync(
            new SmsRequest(PhoneNumber: "+15557654321", Message: "ping", FromNumber: null),
            CancellationToken.None);

        Assert.True(resp.Success);
        Assert.Equal("SM123", resp.MessageId);
        Assert.NotNull(captured);
        Assert.Equal(global::Twilio.Http.HttpMethod.Post, captured!.Method);
        Assert.Contains("/Messages.json", captured.Uri.ToString());
        Assert.Equal("+15557654321", captured.PostParams.First(p => p.Key == "To").Value);
        Assert.Equal("+15551234567", captured.PostParams.First(p => p.Key == "From").Value);
        Assert.Equal("ping", captured.PostParams.First(p => p.Key == "Body").Value);
    }

    [Fact]
    public async Task SendSmsAsync_HonoursPerRequest_FromNumber_OverDefault()
    {
        var client = Substitute.For<ITwilioRestClient>();
        client.AccountSid.Returns("AC0000000000000000000000000000");
        Request? captured = null;
        client.RequestAsync(Arg.Do<Request>(r => captured = r))
            .Returns(new Response(HttpStatusCode.Created, """{"sid":"SM456"}"""));

        await NewProvider(client).SendSmsAsync(
            new SmsRequest("+15557654321", "ping", FromNumber: "+15559999999"),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("+15559999999", captured!.PostParams.First(p => p.Key == "From").Value);
    }

    [Fact]
    public async Task SendSmsAsync_TwilioThrows_ReturnsFailureWithMessage()
    {
        var client = Substitute.For<ITwilioRestClient>();
        client.AccountSid.Returns("AC0000000000000000000000000000");
        client.RequestAsync(Arg.Any<Request>())
            .Returns<Task<Response>>(_ => throw new InvalidOperationException("rate limited"));

        var resp = await NewProvider(client).SendSmsAsync(
            new SmsRequest("+15557654321", "ping", null),
            CancellationToken.None);

        Assert.False(resp.Success);
        Assert.Null(resp.MessageId);
        Assert.Equal("rate limited", resp.ErrorMessage);
    }
}

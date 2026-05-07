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
using FireflyFramework.Notifications.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

public class NotificationDispatcherTests
{
    private sealed class StubEmailProvider : IEmailProvider
    {
        public List<EmailRequest> Calls { get; } = new();
        public Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
        {
            Calls.Add(request);
            return Task.FromResult(new EmailResponse(Guid.NewGuid().ToString(), true, null));
        }
    }

    private sealed class StubSmsProvider : ISmsProvider
    {
        public List<SmsRequest> Calls { get; } = new();
        public Task<SmsResponse> SendSmsAsync(SmsRequest request, CancellationToken ct = default)
        {
            Calls.Add(request);
            return Task.FromResult(new SmsResponse(Guid.NewGuid().ToString(), true, null));
        }
    }

    [Fact]
    public async Task Dispatcher_sends_email_when_no_provider_returns_failure()
    {
        var dispatcher = new NotificationDispatcher(NullLogger<NotificationDispatcher>.Instance);
        var resp = await dispatcher.SendEmailAsync("u1", new EmailRequest("from@x.com", new[] { "to@x.com" }, null, null, "s", null, "<p>hi</p>"));
        resp.Success.Should().BeFalse();
        resp.ErrorMessage.Should().Contain("no email provider");
    }

    [Fact]
    public async Task Dispatcher_routes_to_provider_when_no_preferences_set()
    {
        var stub = new StubEmailProvider();
        var dispatcher = new NotificationDispatcher(
            NullLogger<NotificationDispatcher>.Instance,
            email: new EmailService(stub));

        var resp = await dispatcher.SendEmailAsync("u1",
            new EmailRequest("from@x.com", new[] { "to@x.com" }, null, null, "s", null, "<p>hi</p>"));

        resp.Success.Should().BeTrue();
        stub.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Dispatcher_blocks_send_when_user_disables_channel()
    {
        var stub = new StubEmailProvider();
        var prefs = new InMemoryNotificationPreferenceService();
        await prefs.UpdateAsync(new NotificationPreferenceDto("u1", EmailEnabled: false, SmsEnabled: true, PushEnabled: true));

        var dispatcher = new NotificationDispatcher(
            NullLogger<NotificationDispatcher>.Instance,
            email: new EmailService(stub),
            preferences: prefs);

        var resp = await dispatcher.SendEmailAsync("u1",
            new EmailRequest("from@x.com", new[] { "to@x.com" }, null, null, "s", null, "<p>hi</p>"));

        resp.Success.Should().BeFalse();
        resp.ErrorMessage.Should().Contain("disabled by user preferences");
        stub.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task PreferenceService_round_trip()
    {
        var prefs = new InMemoryNotificationPreferenceService();
        (await prefs.GetAsync("u1")).Should().BeNull();
        (await prefs.IsChannelEnabledAsync("u1", "email")).Should().BeTrue(); // default allow

        await prefs.UpdateAsync(new NotificationPreferenceDto("u1", false, true, false));
        (await prefs.IsChannelEnabledAsync("u1", "email")).Should().BeFalse();
        (await prefs.IsChannelEnabledAsync("u1", "sms")).Should().BeTrue();
        (await prefs.IsChannelEnabledAsync("u1", "push")).Should().BeFalse();
    }

    [Fact]
    public async Task Sms_dispatcher_routes_to_provider()
    {
        var stub = new StubSmsProvider();
        var dispatcher = new NotificationDispatcher(
            NullLogger<NotificationDispatcher>.Instance,
            sms: new SmsService(stub));

        var resp = await dispatcher.SendSmsAsync("u1", new SmsRequest("+34..", "hello"));

        resp.Success.Should().BeTrue();
        stub.Calls.Should().HaveCount(1);
    }
}

// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Smtp;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class SmtpProviderTests
{
    [Fact]
    public async Task Returns_failure_response_when_relay_unreachable()
    {
        // Point at a port nobody is listening on so the call fails predictably.
        var options = Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = 1, // reserved port — guaranteed connection failure
            Timeout = TimeSpan.FromMilliseconds(500),
            DefaultFrom = "test@example.com",
        });

        var provider = new SmtpEmailProvider(options, NullLogger<SmtpEmailProvider>.Instance);
        var response = await provider.SendEmailAsync(new EmailRequest(
            From: "test@example.com",
            To: new[] { "to@example.com" },
            Cc: null,
            Bcc: null,
            Subject: "subject",
            Text: "body",
            Html: null));

        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
        response.MessageId.Should().BeNull();
    }
}

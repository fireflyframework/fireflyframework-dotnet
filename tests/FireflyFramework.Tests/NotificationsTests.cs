using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Core;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

internal sealed class FakeEmailProvider : IEmailProvider
{
    public List<EmailRequest> Sent { get; } = new();
    public Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default)
    {
        Sent.Add(request);
        return Task.FromResult(new EmailResponse(Guid.NewGuid().ToString("N"), true, null));
    }
}

public class NotificationsTests
{
    [Fact]
    public async Task EmailService_sends_via_provider()
    {
        var provider = new FakeEmailProvider();
        var service = new EmailService(provider);
        var resp = await service.SendAsync(new EmailRequest(
            "from@example.com", new[] { "to@example.com" }, null, null,
            "Hi", "plain", "<b>html</b>"));
        resp.Success.Should().BeTrue();
        provider.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task EmailService_renders_template_via_template_engine()
    {
        var provider = new FakeEmailProvider();
        var engine = new ScribanTemplateEngine((id, _) => Task.FromResult(id == "welcome" ? "Hello {{ name }}!" : ""));
        var service = new EmailService(provider, engine);

        var resp = await service.SendTemplateAsync(
            new EmailTemplateRequest("welcome", new() { ["name"] = "Alice" }, new[] { "to@example.com" }),
            from: "no-reply@example.com",
            subject: "Welcome");

        resp.Success.Should().BeTrue();
        provider.Sent.Single().Html.Should().Be("Hello Alice!");
    }
}

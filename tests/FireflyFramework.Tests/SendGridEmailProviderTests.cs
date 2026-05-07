using System.Net;
using FireflyFramework.Notifications;
using FireflyFramework.Notifications.SendGrid;
using NSubstitute;
using SendGrid;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// NSubstitute-mocked tests for <see cref="SendGridEmailProvider"/>. Verify the
/// <see cref="ISendGridClient"/> handoff (subject, recipients, attachments) and the
/// happy-path / failure-path response parsing.
/// </summary>
public sealed class SendGridEmailProviderTests
{
    private static EmailRequest NewRequest(IReadOnlyList<EmailAttachment>? attachments = null) =>
        new(
            From: "no-reply@example.com",
            To: new[] { "alice@example.com" },
            Cc: new[] { "carbon@example.com" },
            Bcc: null,
            Subject: "Welcome",
            Text: "hi",
            Html: "<p>hi</p>",
            Attachments: attachments);

    private static Response Ok(string messageId) => new(
        HttpStatusCode.Accepted,
        new StringContent(string.Empty),
        new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Headers = { { "X-Message-Id", messageId } },
        }.Headers);

    private static Response Failure() => new(
        HttpStatusCode.BadRequest,
        new StringContent("invalid recipient"),
        new HttpResponseMessage(HttpStatusCode.BadRequest).Headers);

    [Fact]
    public async Task SendEmailAsync_HappyPath_ReturnsSuccessAndMessageId()
    {
        var client = Substitute.For<ISendGridClient>();
        client.SendEmailAsync(Arg.Any<global::SendGrid.Helpers.Mail.SendGridMessage>(), Arg.Any<CancellationToken>())
            .Returns(Ok("msg-123"));

        var provider = new SendGridEmailProvider(client);
        var resp = await provider.SendEmailAsync(NewRequest(), CancellationToken.None);

        Assert.True(resp.Success);
        Assert.Equal("msg-123", resp.MessageId);
        Assert.Null(resp.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_ApiError_ReturnsFailureWithBodyAsMessage()
    {
        var client = Substitute.For<ISendGridClient>();
        client.SendEmailAsync(Arg.Any<global::SendGrid.Helpers.Mail.SendGridMessage>(), Arg.Any<CancellationToken>())
            .Returns(Failure());

        var provider = new SendGridEmailProvider(client);
        var resp = await provider.SendEmailAsync(NewRequest(), CancellationToken.None);

        Assert.False(resp.Success);
        Assert.Equal("invalid recipient", resp.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_PassesSubjectRecipientsAndContent_ToSdkMessage()
    {
        global::SendGrid.Helpers.Mail.SendGridMessage? captured = null;
        var client = Substitute.For<ISendGridClient>();
        client.SendEmailAsync(Arg.Do<global::SendGrid.Helpers.Mail.SendGridMessage>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Ok("msg"));

        var provider = new SendGridEmailProvider(client);
        await provider.SendEmailAsync(NewRequest(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Welcome", captured!.Subject);
        Assert.Equal("no-reply@example.com", captured.From.Email);
        Assert.Equal("hi", captured.PlainTextContent);
        Assert.Equal("<p>hi</p>", captured.HtmlContent);
        // SendGrid lifts To and Cc into Personalizations; either property exposes them.
        Assert.NotEmpty(captured.Personalizations);
        var p = captured.Personalizations[0];
        Assert.Contains(p.Tos, e => e.Email == "alice@example.com");
        Assert.Contains(p.Ccs, e => e.Email == "carbon@example.com");
    }

    [Fact]
    public async Task SendEmailAsync_AttachesBytes_AsBase64()
    {
        global::SendGrid.Helpers.Mail.SendGridMessage? captured = null;
        var client = Substitute.For<ISendGridClient>();
        client.SendEmailAsync(Arg.Do<global::SendGrid.Helpers.Mail.SendGridMessage>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Ok("msg"));

        var provider = new SendGridEmailProvider(client);
        var data = new byte[] { 0x01, 0x02, 0x03 };
        await provider.SendEmailAsync(
            NewRequest(new[] { new EmailAttachment("note.txt", "text/plain", data) }),
            CancellationToken.None);

        Assert.NotNull(captured);
        var att = Assert.Single(captured!.Attachments);
        Assert.Equal("note.txt", att.Filename);
        Assert.Equal(Convert.ToBase64String(data), att.Content);
        Assert.Equal("text/plain", att.Type);
    }

    [Fact]
    public void Constructor_RejectsNullClient() =>
        Assert.Throws<ArgumentNullException>(() => new SendGridEmailProvider((ISendGridClient)null!));
}

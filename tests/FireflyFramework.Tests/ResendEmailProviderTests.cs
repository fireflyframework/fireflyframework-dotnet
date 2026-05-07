using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Resend;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="ResendEmailProvider"/>. Stand up a fake
/// <c>https://api.resend.com</c>, point the provider at it, and verify the
/// <c>POST /emails</c> request shape, the bearer-token wiring, and both the
/// happy-path and error-response handling.
/// </summary>
public sealed class ResendEmailProviderTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly ResendEmailProvider _provider;

    public ResendEmailProviderTests()
    {
        _server = WireMockServer.Start();
        var options = Options.Create(new ResendOptions
        {
            ApiKey = "test-key",
            BaseUrl = _server.Urls[0],
        });
        _provider = new ResendEmailProvider(new HttpClient(), options);
    }

    [Fact]
    public async Task SendEmailAsync_HappyPath_ParsesIdFromResponse()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/emails")
                .WithHeader("Authorization", "Bearer test-key"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id":"email-abc"}"""));

        var resp = await _provider.SendEmailAsync(
            new EmailRequest(
                From: "no-reply@example.com",
                To: new[] { "alice@example.com" },
                Cc: null,
                Bcc: null,
                Subject: "Welcome",
                Text: "hi",
                Html: null,
                Attachments: null),
            CancellationToken.None);

        Assert.True(resp.Success);
        Assert.Equal("email-abc", resp.MessageId);
        Assert.Null(resp.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_ApiError_ReturnsFailureWithBodyAsMessage()
    {
        _server.Given(Request.Create().UsingPost().WithPath("/emails"))
            .RespondWith(Response.Create()
                .WithStatusCode(422)
                .WithBody("invalid recipient"));

        var resp = await _provider.SendEmailAsync(
            new EmailRequest(
                From: "from@x.com",
                To: new[] { "bad" },
                Cc: null,
                Bcc: null,
                Subject: "s",
                Text: "t",
                Html: null,
                Attachments: null),
            CancellationToken.None);

        Assert.False(resp.Success);
        Assert.Null(resp.MessageId);
        Assert.Equal("invalid recipient", resp.ErrorMessage);
    }

    public void Dispose() => _server.Dispose();
}

using FireflyFramework.Notifications;
using FireflyFramework.Notifications.Firebase;
using FirebaseAdmin.Messaging;
using NSubstitute;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// NSubstitute-mocked tests for <see cref="FcmPushProvider"/>. The Firebase Admin SDK relies on
/// <c>FirebaseMessaging.DefaultInstance</c>, a static singleton that requires a real Firebase
/// project to instantiate. The provider therefore exposes <see cref="IFirebaseMessenger"/> as a
/// thin seam: production wires <c>DefaultFirebaseMessenger</c>; tests substitute a fake to assert
/// the <see cref="Message"/> shape the adapter constructs and the response handling.
/// </summary>
public sealed class FcmPushProviderTests
{
    [Fact]
    public async Task SendPushAsync_HappyPath_ConstructsMessage_AndReturnsId()
    {
        Message? captured = null;
        var messenger = Substitute.For<IFirebaseMessenger>();
        messenger.SendAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns("projects/firefly/messages/abc");

        var provider = new FcmPushProvider(messenger);

        var resp = await provider.SendPushAsync(
            new PushNotificationRequest(
                Token: "device-1",
                Title: "Hello",
                Body: "World",
                Data: new Dictionary<string, string> { ["k"] = "v" }),
            CancellationToken.None);

        Assert.True(resp.Success);
        Assert.Equal("projects/firefly/messages/abc", resp.MessageId);
        Assert.NotNull(captured);
        Assert.Equal("device-1", captured!.Token);
        Assert.Equal("Hello", captured.Notification.Title);
        Assert.Equal("World", captured.Notification.Body);
        Assert.Equal("v", captured.Data["k"]);
    }

    [Fact]
    public async Task SendPushAsync_NullData_PassesEmptyDictionary()
    {
        Message? captured = null;
        var messenger = Substitute.For<IFirebaseMessenger>();
        messenger.SendAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns("id");

        await new FcmPushProvider(messenger).SendPushAsync(
            new PushNotificationRequest("t", "title", "body", null),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.NotNull(captured!.Data);
        Assert.Empty(captured.Data);
    }

    [Fact]
    public async Task SendPushAsync_MessengerThrows_ReturnsFailureWithExceptionMessage()
    {
        var messenger = Substitute.For<IFirebaseMessenger>();
        messenger.SendAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("invalid token"));

        var resp = await new FcmPushProvider(messenger).SendPushAsync(
            new PushNotificationRequest("bad", "t", "b", null),
            CancellationToken.None);

        Assert.False(resp.Success);
        Assert.Null(resp.MessageId);
        Assert.Equal("invalid token", resp.ErrorMessage);
    }

    [Fact]
    public void Constructor_RejectsNullMessenger() =>
        Assert.Throws<ArgumentNullException>(() => new FcmPushProvider((IFirebaseMessenger)null!));
}

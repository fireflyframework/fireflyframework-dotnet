using FireflyFramework.Notifications;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Notifications.Firebase;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firefly:Notifications:Firebase";
    public string? CredentialsPath { get; set; }
    public string? ProjectId { get; set; }
}

public sealed class FcmPushProvider : IPushProvider
{
    public FcmPushProvider(IOptions<FirebaseOptions> options)
    {
        if (FirebaseApp.DefaultInstance is null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = options.Value.CredentialsPath is null
                    ? GoogleCredential.GetApplicationDefault()
                    : GoogleCredential.FromFile(options.Value.CredentialsPath),
                ProjectId = options.Value.ProjectId,
            });
        }
    }

    public async Task<PushNotificationResponse> SendPushAsync(PushNotificationRequest request, CancellationToken ct = default)
    {
        try
        {
            var message = new Message
            {
                Token = request.Token,
                Notification = new Notification { Title = request.Title, Body = request.Body },
                Data = request.Data ?? new Dictionary<string, string>(),
            };

            var id = await FirebaseMessaging.DefaultInstance.SendAsync(message, ct).ConfigureAwait(false);
            return new PushNotificationResponse(id, true, null);
        }
        catch (Exception ex)
        {
            return new PushNotificationResponse(null, false, ex.Message);
        }
    }
}

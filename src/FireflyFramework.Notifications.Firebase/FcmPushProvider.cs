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

/// <summary>
/// Tiny seam over <see cref="FirebaseMessaging"/>. The default implementation forwards to
/// <c>FirebaseMessaging.DefaultInstance.SendAsync</c>; tests substitute a fake to assert the
/// <see cref="Message"/> shape without standing up a real Firebase project.
/// </summary>
public interface IFirebaseMessenger
{
    Task<string> SendAsync(Message message, CancellationToken ct);
}

internal sealed class DefaultFirebaseMessenger : IFirebaseMessenger
{
    public Task<string> SendAsync(Message message, CancellationToken ct) =>
        FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
}

public sealed class FcmPushProvider : IPushProvider
{
    private readonly IFirebaseMessenger _messenger;

    /// <summary>
    /// Production constructor — initialises the default <see cref="FirebaseApp"/> from the configured
    /// credentials and routes sends through the real <see cref="FirebaseMessaging"/> singleton.
    /// </summary>
    public FcmPushProvider(IOptions<FirebaseOptions> options)
        : this(EnsureDefaultApp(options.Value)) { }

    /// <summary>
    /// Testing / advanced-DI constructor — accepts a custom <see cref="IFirebaseMessenger"/>. Use this
    /// to substitute a fake in unit tests or to wire up a non-default <see cref="FirebaseApp"/>.
    /// </summary>
    public FcmPushProvider(IFirebaseMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _messenger = messenger;
    }

    private static IFirebaseMessenger EnsureDefaultApp(FirebaseOptions options)
    {
        if (FirebaseApp.DefaultInstance is null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = options.CredentialsPath is null
                    ? GoogleCredential.GetApplicationDefault()
                    : GoogleCredential.FromFile(options.CredentialsPath),
                ProjectId = options.ProjectId,
            });
        }

        return new DefaultFirebaseMessenger();
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

            var id = await _messenger.SendAsync(message, ct).ConfigureAwait(false);
            return new PushNotificationResponse(id, true, null);
        }
        catch (Exception ex)
        {
            return new PushNotificationResponse(null, false, ex.Message);
        }
    }
}

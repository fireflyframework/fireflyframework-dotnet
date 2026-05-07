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

namespace FireflyFramework.Notifications;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Data);

public sealed record EmailRequest(
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc,
    string Subject,
    string? Text,
    string? Html,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public sealed record EmailResponse(string? MessageId, bool Success, string? ErrorMessage);

public sealed record SmsRequest(string PhoneNumber, string Message, string? FromNumber = null);
public sealed record SmsResponse(string? MessageId, bool Success, string? ErrorMessage);

public sealed record PushNotificationRequest(string Token, string Title, string Body, Dictionary<string, string>? Data = null);
public sealed record PushNotificationResponse(string? MessageId, bool Success, string? ErrorMessage);

public sealed record EmailTemplateRequest(string TemplateId, Dictionary<string, object?> Variables, IReadOnlyList<string> Recipients);

public enum EmailStatus { Pending, Sent, Failed, Bounced }

public interface IEmailProvider
{
    Task<EmailResponse> SendEmailAsync(EmailRequest request, CancellationToken ct = default);
}

public interface ISmsProvider
{
    Task<SmsResponse> SendSmsAsync(SmsRequest request, CancellationToken ct = default);
}

public interface IPushProvider
{
    Task<PushNotificationResponse> SendPushAsync(PushNotificationRequest request, CancellationToken ct = default);
}

public sealed record NotificationPreferenceDto(string UserId, bool EmailEnabled, bool SmsEnabled, bool PushEnabled);

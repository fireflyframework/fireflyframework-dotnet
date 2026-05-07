using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace FireflyFramework.Idp.AzureAd;

/// <summary>
/// Microsoft Graph admin client used by <see cref="AzureAdIdpAdapter"/> for user CRUD,
/// password reset and group/role assignment. Auths via the configured app registration
/// using <see cref="ClientSecretCredential"/>. Mirrors Java <c>EntraIdAdminService</c>.
/// </summary>
public sealed class AzureAdGraphAdmin
{
    private readonly GraphServiceClient _graph;

    public AzureAdGraphAdmin(IOptions<AzureAdOptions> options)
    {
        var opt = options.Value;
        if (opt.ClientSecret is null)
        {
            throw new InvalidOperationException("Microsoft Graph admin requires AzureAdOptions.ClientSecret.");
        }

        var credential = new ClientSecretCredential(opt.TenantId, opt.ClientId, opt.ClientSecret);
        _graph = new GraphServiceClient(credential, scopes: new[] { "https://graph.microsoft.com/.default" });
    }

    public async Task<string> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new User
        {
            AccountEnabled = true,
            DisplayName = $"{request.GivenName} {request.FamilyName}".Trim(),
            MailNickname = request.Username,
            UserPrincipalName = request.Email,
            Mail = request.Email,
            GivenName = request.GivenName,
            Surname = request.FamilyName,
            PasswordProfile = request.Password is null ? null : new PasswordProfile
            {
                Password = request.Password,
                ForceChangePasswordNextSignIn = false,
            },
        };

        var created = await _graph.Users.PostAsync(user, cancellationToken: ct).ConfigureAwait(false);
        return created?.Id ?? throw new InvalidOperationException("Microsoft Graph did not return a user id");
    }

    public async Task UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var patch = new User
        {
            DisplayName = $"{request.GivenName} {request.FamilyName}".Trim(),
            GivenName = request.GivenName,
            Surname = request.FamilyName,
            Mail = request.Email,
        };

        await _graph.Users[userId].PatchAsync(patch, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        await _graph.Users[userId].DeleteAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
    {
        var patch = new User
        {
            PasswordProfile = new PasswordProfile
            {
                Password = newPassword,
                ForceChangePasswordNextSignIn = true,
            },
        };

        await _graph.Users[userId].PatchAsync(patch, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListGroupsAsync(CancellationToken ct = default)
    {
        var page = await _graph.Groups.GetAsync(cancellationToken: ct).ConfigureAwait(false);
        return page?.Value?.Select(g => g.DisplayName ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList()
            ?? new List<string>();
    }

    public async Task AssignToGroupAsync(string userId, string groupId, CancellationToken ct = default)
    {
        var reference = new Microsoft.Graph.Models.ReferenceCreate
        {
            OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{userId}",
        };
        await _graph.Groups[groupId].Members.Ref.PostAsync(reference, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task RemoveFromGroupAsync(string userId, string groupId, CancellationToken ct = default)
    {
        await _graph.Groups[groupId].Members[userId].Ref.DeleteAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<UserInfoResponse?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _graph.Users[userId].GetAsync(cancellationToken: ct).ConfigureAwait(false);
        if (user is null) return null;
        return new UserInfoResponse(user.Id ?? userId, user.Mail ?? user.UserPrincipalName,
            user.GivenName, user.Surname, null, null);
    }

    /// <summary>
    /// Revokes all refresh tokens / sign-in sessions for the user. Hits the Graph
    /// <c>POST /users/{id}/revokeSignInSessions</c> action.
    /// </summary>
    public async Task RevokeSignInSessionsAsync(string userId, CancellationToken ct = default)
    {
        await _graph.Users[userId].RevokeSignInSessions.PostAsRevokeSignInSessionsPostResponseAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<string> CreateGroupAsync(string displayName, string? description, CancellationToken ct = default)
    {
        var group = new Group
        {
            DisplayName = displayName,
            MailEnabled = false,
            MailNickname = displayName.Replace(' ', '_'),
            SecurityEnabled = true,
            Description = description,
        };

        var created = await _graph.Groups.PostAsync(group, cancellationToken: ct).ConfigureAwait(false);
        return created?.Id ?? throw new InvalidOperationException("Microsoft Graph did not return a group id");
    }
}

using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Sdk;

/// <summary>
/// Typed contract for the callback management REST API exposed by
/// <c>FireflyFramework.Callbacks.Web</c>. All methods map one-for-one
/// onto the controller surface in <c>CallbackConfigurationController</c>.
/// </summary>
public interface ICallbackClient
{
    Task<IReadOnlyList<CallbackConfigurationDto>?> ListAsync(string? tenantId = null, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> UpdateAsync(Guid id, CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

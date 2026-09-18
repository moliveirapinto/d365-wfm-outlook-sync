using Azure.Core;
using Azure.Identity;
using WfmOutlookSync.Configuration;

namespace WfmOutlookSync.Auth;

/// <summary>
/// Builds an Azure.Identity <see cref="TokenCredential"/> for either Dataverse or Graph
/// based purely on configuration - no auth mode, tenant, or credential is ever hardcoded.
/// ManagedIdentity is the recommended, secret-free mode for anything deployed to Azure.
/// </summary>
public static class CredentialFactory
{
    public static TokenCredential Create(DataverseOptions options) => Create(
        options.AuthMode, options.TenantId, options.ClientId, options.ClientSecret, options.UserAssignedManagedIdentityClientId);

    public static TokenCredential Create(GraphOptions options) => Create(
        options.AuthMode, options.TenantId, options.ClientId, options.ClientSecret, options.UserAssignedManagedIdentityClientId);

    private static TokenCredential Create(
        AuthMode mode, string? tenantId, string? clientId, string? clientSecret, string? userAssignedManagedIdentityClientId)
    {
        return mode switch
        {
            AuthMode.ManagedIdentity => string.IsNullOrWhiteSpace(userAssignedManagedIdentityClientId)
                ? new ManagedIdentityCredential()
                : new ManagedIdentityCredential(userAssignedManagedIdentityClientId),

            AuthMode.ClientSecret => new ClientSecretCredential(
                RequireValue(tenantId, nameof(tenantId)),
                RequireValue(clientId, nameof(clientId)),
                RequireValue(clientSecret, nameof(clientSecret))),

            _ => throw new NotSupportedException($"Unsupported auth mode '{mode}'.")
        };
    }

    private static string RequireValue(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"'{name}' is required when AuthMode is ClientSecret.")
            : value;
}

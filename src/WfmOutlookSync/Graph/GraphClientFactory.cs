using Azure.Core;
using Microsoft.Graph;

namespace WfmOutlookSync.Graph;

/// <summary>Builds the application-permission <see cref="GraphServiceClient"/> used for all Outlook calendar access.</summary>
public static class GraphClientFactory
{
    private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };

    public static GraphServiceClient Create(TokenCredential credential) => new(credential, Scopes);
}

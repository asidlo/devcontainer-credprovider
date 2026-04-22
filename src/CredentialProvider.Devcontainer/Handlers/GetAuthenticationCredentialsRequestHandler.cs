// NuGet Credential Provider Plugin for Devcontainers
// Handles GetAuthenticationCredentials requests

using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Handlers;

/// <summary>
/// Handles GetAuthenticationCredentials requests - provides credentials for Azure Artifacts feeds
/// </summary>
public sealed class GetAuthenticationCredentialsRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public async Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<GetAuthenticationCredentialsRequest>(request);
        var uri = payload?.Uri?.AbsoluteUri;

        Program.Log($"GetAuthenticationCredentials: URI={uri}, IsRetry={payload?.IsRetry}");

        if (string.IsNullOrEmpty(uri) || !Program.IsAzureDevOpsUri(uri))
        {
            // Return error for non-Azure DevOps URIs - NuGet will try other providers
            var notApplicableResponse = new GetAuthenticationCredentialsResponse(
                username: null,
                password: null,
                message: "Not an Azure DevOps feed",
                authenticationTypes: null,
                responseCode: MessageResponseCode.NotFound);

            await responseHandler.SendResponseAsync(request, notApplicableResponse, cancellationToken);
            return;
        }

        // Pass cancellation token to token acquisition
        var token = await Program.TryGetAccessTokenAsync(uri, cancellationToken);

        if (string.IsNullOrEmpty(token))
        {
            Program.Log("Auth helper not available, returning NotApplicable for fallback to other providers");

            // Return NotApplicable to allow NuGet to try other credential providers
            // (like Microsoft's artifacts-credprovider which supports device code flow)
            var notApplicableResponse = new GetAuthenticationCredentialsResponse(
                username: null,
                password: null,
                message: "Auth helper not available. Falling back to other credential providers.",
                authenticationTypes: null,
                responseCode: MessageResponseCode.NotFound);

            await responseHandler.SendResponseAsync(request, notApplicableResponse, cancellationToken);
            return;
        }

        Program.Log("Successfully acquired credentials");

        var successResponse = new GetAuthenticationCredentialsResponse(
            username: "DevcontainerCredProvider",
            password: token,
            message: null,
            authenticationTypes: new List<string> { "Basic" },
            responseCode: MessageResponseCode.Success);

        await responseHandler.SendResponseAsync(request, successResponse, cancellationToken);
    }
}

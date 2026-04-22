// NuGet Credential Provider Plugin for Devcontainers
// Disabled handler for GetAuthenticationCredentials requests

using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Handlers;

/// <summary>
/// Disabled handler for GetAuthenticationCredentials - always returns NotApplicable to force fallback
/// </summary>
public sealed class DisabledGetAuthenticationCredentialsRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<GetAuthenticationCredentialsRequest>(request);
        Program.Log($"DISABLED - Returning NotApplicable for: {payload?.Uri}");

        var response = new GetAuthenticationCredentialsResponse(
            username: null,
            password: null,
            message: "Plugin disabled via DEVCONTAINER_CREDPROVIDER_DISABLED or config file. Falling back to other providers.",
            authenticationTypes: null,
            responseCode: MessageResponseCode.NotFound);

        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

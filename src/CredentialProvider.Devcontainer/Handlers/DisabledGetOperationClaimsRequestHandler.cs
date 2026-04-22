// NuGet Credential Provider Plugin for Devcontainers
// Disabled handler for GetOperationClaims requests

using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Handlers;

/// <summary>
/// Disabled handler for GetOperationClaims - returns empty claims to indicate no capability
/// </summary>
public sealed class DisabledGetOperationClaimsRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<GetOperationClaimsRequest>(request);
        Program.Log($"DISABLED - Returning no claims for: '{payload?.PackageSourceRepository}'");

        // Return empty claims - we don't claim to handle anything when disabled
        var response = new GetOperationClaimsResponse(new List<OperationClaim>());

        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

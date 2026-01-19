// NuGet Credential Provider Plugin for Devcontainers
// Handles GetOperationClaims requests

using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Handlers;

/// <summary>
/// Handles GetOperationClaims requests - tells NuGet what operations this plugin supports
/// </summary>
public sealed class GetOperationClaimsRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<GetOperationClaimsRequest>(request);
        var packageSourceUri = payload?.PackageSourceRepository;

        Program.Log($"GetOperationClaims: PackageSourceRepository='{packageSourceUri}'");

        // When package source is null/empty, NuGet is asking for source-agnostic operations
        // Return Authentication to indicate we handle auth
        // When package source is specified, only claim it if it's Azure DevOps
        var claims = new List<OperationClaim>();

        if (string.IsNullOrEmpty(packageSourceUri))
        {
            // Source-agnostic query - we support Authentication generally
            claims.Add(OperationClaim.Authentication);
            Program.Log("Claiming Authentication (source-agnostic)");
        }
        else if (Program.IsAzureDevOpsUri(packageSourceUri))
        {
            // Source-specific query - we handle Azure DevOps feeds
            claims.Add(OperationClaim.Authentication);
            Program.Log("Claiming Authentication for Azure DevOps");
        }
        else
        {
            Program.Log("Not claiming (not Azure DevOps)");
        }

        var response = new GetOperationClaimsResponse(claims);
        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

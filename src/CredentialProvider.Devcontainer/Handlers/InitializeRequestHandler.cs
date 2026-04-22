// NuGet Credential Provider Plugin for Devcontainers
// Handles Initialize requests

using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Handlers;

/// <summary>
/// Handles Initialize requests - called by NuGet to initialize the plugin
/// </summary>
public sealed class InitializeRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<InitializeRequest>(request);
        Program.Log($"Initialize: ClientVersion={payload?.ClientVersion}, Culture={payload?.Culture}");

        // Update connection timeout if provided
        if (payload?.RequestTimeout != null)
        {
            connection.Options.SetRequestTimeout(payload.RequestTimeout);
        }

        var response = new InitializeResponse(MessageResponseCode.Success);
        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

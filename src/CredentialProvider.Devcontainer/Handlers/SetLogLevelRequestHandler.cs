// NuGet Credential Provider Plugin for Devcontainers
// Handles SetLogLevel requests

using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Handlers;

/// <summary>
/// Handles SetLogLevel requests
/// </summary>
public sealed class SetLogLevelRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        var payload = MessageUtilities.DeserializePayload<SetLogLevelRequest>(request);
        Program.Log($"SetLogLevel: {payload?.LogLevel}");

        var response = new SetLogLevelResponse(MessageResponseCode.Success);
        return responseHandler.SendResponseAsync(request, response, cancellationToken);
    }
}

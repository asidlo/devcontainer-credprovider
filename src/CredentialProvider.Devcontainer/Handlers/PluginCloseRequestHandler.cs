// NuGet Credential Provider Plugin for Devcontainers
// Handles Close requests

using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Handlers;

/// <summary>
/// Handles Close requests - called by NuGet when shutting down the plugin
/// </summary>
public sealed class PluginCloseRequestHandler : IRequestHandler
{
    public CancellationToken CancellationToken => CancellationToken.None;

    public Task HandleResponseAsync(
        IConnection connection,
        Message request,
        IResponseHandler responseHandler,
        CancellationToken cancellationToken)
    {
        // Acknowledge the close request
        // The plugin will exit after responding
        return Task.CompletedTask;
    }
}

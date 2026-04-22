using CredentialProvider.Devcontainer.Handlers;
using NuGet.Protocol.Plugins;

namespace CredentialProvider.Devcontainer.Tests;

/// <summary>
/// Tests verifying that all handlers implement IRequestHandler correctly.
/// </summary>
public class RequestHandlerTests
{
    [Fact]
    public void AllHandlers_ImplementIRequestHandler()
    {
        // Verify all handler types implement IRequestHandler
        var handlerTypes = new[]
        {
            typeof(GetOperationClaimsRequestHandler),
            typeof(GetAuthenticationCredentialsRequestHandler),
            typeof(SetLogLevelRequestHandler),
            typeof(InitializeRequestHandler),
            typeof(PluginCloseRequestHandler),
            typeof(DisabledGetAuthenticationCredentialsRequestHandler),
            typeof(DisabledGetOperationClaimsRequestHandler)
        };

        foreach (var type in handlerTypes)
        {
            var handler = Activator.CreateInstance(type);
            Assert.IsAssignableFrom<IRequestHandler>(handler);
        }
    }
}

namespace NetDaemon.Client.Internal;

internal class HomeAssistantConnectionFactory(ILogger<IHomeAssistantConnection> logger,
    IHomeAssistantApiManager apiManager, JsonSerializerOptions serializerOptions) : IHomeAssistantConnectionFactory
{
    public IHomeAssistantConnection New(IWebSocketClientTransportPipeline transportPipeline)
    {
        return new HomeAssistantConnection(logger, transportPipeline, apiManager,serializerOptions);
    }
}

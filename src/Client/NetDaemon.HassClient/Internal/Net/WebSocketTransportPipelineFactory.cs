using NetDaemon.HassModel;

namespace NetDaemon.Client.Internal.Net;

//internal class WebSocketClientTransportPipelineFactory(IOptions<HomeAssistantSettings> settings, ILoggerFactory loggerFactory) : IWebSocketClientTransportPipelineFactory
//{
//    public IWebSocketClientTransportPipeline New(IWebSocketClient webSocketClient)
//    {
//        if (webSocketClient.State != WebSocketState.Open)
//            throw new ApplicationException("Unexpected state of WebSocketClient, should be 'Open'");
//        if (settings.Value.EnableSocketLogging)
//        {
//            return new ProtoolLoggingWebSocketClientTransportPipeline(webSocketClient, loggerFactory.CreateLogger<ProtoolLoggingWebSocketClientTransportPipeline>());

        return new WebSocketClientTransportPipeline(webSocketClient,HassJsonContext.DefaultOptions);
    }
}

using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Net;

namespace NSpec.ContinuousRunner
{
    public class NeedsRefreshWebSocketsServer : WebSocketsServer
    {
        public override string ServerName => "NeedsRefreshServer";

        public void Refresh()
        {
            foreach (var ws in WebSockets)
                Send(ws, "refresh");
        }

        protected override void OnClientConnected(WebSocketContext context)
        {
        }

        protected override void OnClientDisconnected(WebSocketContext context)
        {
        }

        protected override void OnFrameReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult)
        {
        }

        protected override void OnMessageReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult)
        {
        }
    }
}
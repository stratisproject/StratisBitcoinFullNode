using Microsoft.AspNetCore.SignalR;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public class FullNodeHub : Hub
    {
        private readonly IWebSocketService webSocketService;
        private readonly FullNode fullNode;

        public FullNodeHub(IWebSocketService webSocketService, FullNode fullNode)
        {
            this.fullNode = fullNode;
            this.webSocketService = webSocketService;
        }

        public void Subscribe(string channel)
        {
            // TODO: Do something like this.
            //this.webSocketService.Subscribe(channel);
        }

        public void BroadcastMessage(string name, string message)
        {
            if (message == "stats")
            {
                Command(message);
                return;
            }

            this.Clients.All.SendAsync("broadcastMessage", name, message);
        }

        public void Command(string commandName)
        {
            if (commandName == "stats")
            {
                this.Clients.Caller.SendAsync("broadcastMessage", commandName, this.fullNode.LastLogOutput);
            }
        }

        public void Echo(string name, string message)
        {
            this.Clients.Client(this.Context.ConnectionId).SendAsync("echo", name, message + " (echo from server)");
        }
    }
}

using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    public class ConnectionManagerController : BaseRPCController
    {
        public ConnectionManagerController(IConnectionManager connectionManager) : base(connectionManager: connectionManager)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
        }

        [ActionName("addnode")]
        public bool AddNode(string endpointStr, string command)
        {
            Guard.NotNull(this.ConnectionManager, nameof(this.ConnectionManager));
            IPEndPoint endpoint = NodeSettings.ConvertToEndpoint(endpointStr, this.ConnectionManager.Network.DefaultPort);
            switch (command)
            {
                case "add":
                    this.ConnectionManager.AddNodeAddress(endpoint);
                    break;
                case "remove":
                    this.ConnectionManager.RemoveNodeAddress(endpoint);
                    break;
                case "onetry":
                    this.ConnectionManager.Connect(endpoint);
                    break;
                default:
                    throw new ArgumentException("command");
            }
            return true;
        }
    }
}

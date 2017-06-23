using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC.Controllers
{
    public class ConnectionManagerController : BaseRPCController
    {
        public ConnectionManagerController(IConnectionManager connectionManager) : base(connectionManager: connectionManager)
        {
            Guard.NotNull(this._ConnectionManager, nameof(_ConnectionManager));
        }

        [ActionName("addnode")]
        public bool AddNode(string endpointStr, string command)
        {
            Guard.NotNull(this._ConnectionManager, nameof(_ConnectionManager));
            IPEndPoint endpoint = NodeSettings.ConvertToEndpoint(endpointStr, this._ConnectionManager.Network.DefaultPort);
            switch (command)
            {
                case "add":
                    this._ConnectionManager.AddNodeAddress(endpoint);
                    break;
                case "remove":
                    this._ConnectionManager.RemoveNodeAddress(endpoint);
                    break;
                case "onetry":
                    this._ConnectionManager.Connect(endpoint);
                    break;
                default:
                    throw new ArgumentException("command");
            }
            return true;
        }
    }
}

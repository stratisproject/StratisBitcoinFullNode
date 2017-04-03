using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Controllers
{
	//TODO: Need to be extensible, should be FullNodeController
	public partial class ConsensusController : Controller
	{
		[ActionName("addnode")]
		public bool AddNode(string endpointStr, string command)
		{
			var endpoint = NodeSettings.ConvertToEndpoint(endpointStr, _FullNode.Network.DefaultPort);
			switch(command)
			{
				case "add":
					_FullNode.ConnectionManager.AddNode(endpoint);
					break;
				case "remove":
					_FullNode.ConnectionManager.RemoveNode(endpoint);
					break;
				case "onetry":
					_FullNode.ConnectionManager.Connect(endpoint);
					break;
				default:
					throw new ArgumentException("command");
			}
			return true;
		}
	}
}

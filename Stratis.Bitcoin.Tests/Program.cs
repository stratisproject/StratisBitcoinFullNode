using Microsoft.Extensions.Logging.Console;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests
{
	class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(n => new ConsoleLogger(n, (a, b) => true, false)));
			new Class1().CanRewind();
		}

		private void WaitReachBlock(FullNode fullNode, int height)
		{
			while(true)
			{
				if(fullNode?.ConsensusLoop?.Tip?.Height >= height)
				{
					break;
				}
			}
		}

		public void ValidSomeBlocksOnMainnet()
		{
			using(NodeContext ctx = NodeContext.Create(network: Network.Main))
			{
				var nodeArgs = new NodeArgs();
				nodeArgs.DataDir = ctx.FolderName;
				nodeArgs.ConnectionManager.Connect.Add(new IPEndPoint(IPAddress.Loopback, ctx.Network.DefaultPort));
				var fullNode = new FullNode(nodeArgs);
				fullNode.Start();
				int increment = 20000;
				int reachNext = increment;
				for(int i = 0; i < 10; i++)
				{
					WaitReachBlock(fullNode, reachNext);
					fullNode = Restart(fullNode);
					reachNext += increment;
				}
				fullNode.ThrowIfUncatchedException();
				fullNode.Dispose();
			}
		}

		private FullNode Restart(FullNode fullNode)
		{
			fullNode.Dispose();
			fullNode.ThrowIfUncatchedException();
			fullNode = new FullNode(fullNode.Args);
			fullNode.Start();
			return fullNode;
		}

		static Random _Rand = new Random();
	}
}

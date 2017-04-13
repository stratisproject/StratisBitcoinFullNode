using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Logging
{
	public class Logs
	{
		static Logs()
		{
			Configure(new LoggerFactory());
		}

		public static void Configure(ILoggerFactory factory)
		{
			Configuration = factory.CreateLogger("Configuration");
			RPC = factory.CreateLogger("RPC");
			FullNode = factory.CreateLogger("FullNode");
			ConnectionManager = factory.CreateLogger("ConnectionManager");
			Bench = factory.CreateLogger("Bench");
			Mempool = factory.CreateLogger("Mempool");
			BlockStore = factory.CreateLogger("BlockStore");
			Consensus = factory.CreateLogger("Consensus");
			EstimateFee = factory.CreateLogger("EstimateFee");
			Mining = factory.CreateLogger("Mining");
			Notifications = factory.CreateLogger("Notifications");
        }

		public static ILogger Configuration
		{
			get; set;
		}
		public static ILogger RPC
		{
			get; set;
		}
		public static ILogger FullNode
		{
			get; set;
		}
		public static ILogger ConnectionManager
		{
			get; set;
		}
		public static ILogger Bench
		{
			get; set;
		}
		public static ILogger Mempool
		{
			get; set;
		}
		public static ILogger BlockStore
		{
			get; set;
		}
		public static ILogger EstimateFee
		{
			get; set;
		}

		public static ILogger Consensus
		{
			get; set;
		}

		public static ILogger Mining
		{
			get; set;
		}

	    public static ILogger Notifications
	    {
	        get; set;
	    }

        public const int ColumnLength = 16;
	}
}

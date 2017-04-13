using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Logging
{
	public class Logs
	{
		public static void Configure(ILoggerFactory factory)
		{
			LoggerFactory = factory;

			// These match namespace; classes can also use CreateLogger<T>, which will inherit
			Configuration = factory.CreateLogger("Stratis.Bitcoin.Configuration");
			RPC = factory.CreateLogger("Stratis.Bitcoin.RPC");
			FullNode = factory.CreateLogger("Stratis.Bitcoin.FullNode");
			ConnectionManager = factory.CreateLogger("Stratis.Bitcoin.Connection");
			Bench = factory.CreateLogger("Stratis.Bitcoin.FullNode.ConsensusStats");
			Mempool = factory.CreateLogger("Stratis.Bitcoin.MemoryPool");
			BlockStore = factory.CreateLogger("Stratis.Bitcoin.BlockStore");
			Consensus = factory.CreateLogger("Stratis.Bitcoin.Consensus");
			EstimateFee = factory.CreateLogger("Stratis.Bitcoin.Fee");
			Mining = factory.CreateLogger("Stratis.Bitcoin.Mining");
		}

		public static ILoggerFactory GetLoggerFactory(string[] args)
		{
			// TODO: preload enough args for -conf= or -datadir= to get debug args from there
      // TODO: currently only takes -debug arg
			var debugArgs = args.Where(a => a.StartsWith("-debug=")).Select(a => a.Substring("-debug=".Length).Replace("\"", "")).FirstOrDefault();

			var keyToCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					//{ "addrman", "" },
					//{ "alert", "" },
					{ "bench", "Stratis.Bitcoin.FullNode.ConsensusStats" },
					//{ "coindb", "" },
					{ "db", "Stratis.Bitcoin.BlockStore" }, 
					//{ "lock", "" }, 
					//{ "rand", "" }, 
					{ "rpc", "Stratis.Bitcoin.RPC" }, 
					//{ "selectcoins", "" }, 
					{ "mempool", "Stratis.Bitcoin.MemoryPool" }, 
					//{ "mempoolrej", "" }, 
					{ "net", "Stratis.Bitcoin.Connection" }, 
					//{ "proxy", "" }, 
					//{ "prune", "" }, 
					//{ "http", "" }, 
					//{ "libevent", "" }, 
					//{ "tor", "" }, 
					//{ "zmq", "" }, 
					//{ "qt", "" },

					// Short Names
					{ "estimatefee", "Stratis.Bitcoin.Fee" },
					{ "configuration", "Stratis.Bitcoin.Configuration" },
					{ "fullnode", "Stratis.Bitcoin.FullNode" },
					{ "consensus", "Stratis.Bitcoin.FullNode" },
					{ "mining", "Stratis.Bitcoin.FullNode" },
				};
			var filterSettings = new FilterLoggerSettings();
			// Default level is Information
			filterSettings.Add("Default", LogLevel.Information);
			// TODO: Probably should have a way to configure these as well
			filterSettings.Add("System", LogLevel.Warning);
			filterSettings.Add("Microsoft", LogLevel.Warning);
			// Disable aspnet core logs (retained from ASP.NET config)
			filterSettings.Add("Microsoft.AspNetCore", LogLevel.Error);

			if (!string.IsNullOrWhiteSpace(debugArgs))
			{
				if (debugArgs.Trim() == "1")
				{
					// Increase all logging to Trace
					filterSettings.Add("Stratis.Bitcoin", LogLevel.Trace);
				}
				else
				{
					// Increase selected categories to Trace
					var categoryKeys = debugArgs.Split(',');
					foreach (var key in categoryKeys)
					{
						string category;
						if (keyToCategory.TryGetValue(key.Trim(), out category))
						{
							filterSettings.Add(category, LogLevel.Trace);
						}
						else
						{
							// Can directly specify something like -debug=Stratis.Bitcoin.Miner
							filterSettings.Add(key, LogLevel.Trace);
						}
					}
				}
			}

			// TODO: Additional args
			//var logipsArgs = args.Where(a => a.StartsWith("-logips=")).Select(a => a.Substring("-logips=".Length).Replace("\"", "")).FirstOrDefault();
			//var printtoconsoleArgs = args.Where(a => a.StartsWith("-printtoconsole=")).Select(a => a.Substring("-printtoconsole=".Length).Replace("\"", "")).FirstOrDefault();

			ILoggerFactory loggerFactory = new LoggerFactory()
				.WithFilter(filterSettings);
			loggerFactory.AddDebug(LogLevel.Trace);
			loggerFactory.AddConsole(LogLevel.Trace);

      // TODO: To add file logging, need to get -datadir / -config from args

			return loggerFactory;
		}

		public static ILogger Configuration
		{
			get; private set;
		}

		public static ILogger RPC
		{
			get; private set;
		}

		public static ILogger FullNode
		{
			get; private set;
		}

		public static ILogger ConnectionManager
		{
			get; private set;
		}

		public static ILogger Bench
		{
			get; private set;
		}

		public static ILogger Mempool
		{
			get; set;
		}
		public static ILogger BlockStore
		{
			get; private set;
		}

		public static ILogger EstimateFee
		{
			get; private set;
		}

		public static ILoggerFactory LoggerFactory
		{
			get; private set;
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

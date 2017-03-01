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
			EstimateFee = factory.CreateLogger("Stratis.Bitcoin.Fee");
		}

		public static ILoggerFactory GetLoggerFactory(string[] args)
		{
			// TODO: preload enough args for -conf= or -datadir= to get debug args from there

			//Configuration = factory.CreateLogger("Configuration");
			//FullNode = factory.CreateLogger("FullNode");
			//ConnectionManager = factory.CreateLogger("ConnectionManager");
			//EstimateFee = factory.CreateLogger("EstimateFee");

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
				};
			var filterLoggerSettings = new FilterLoggerSettings();
			// Default level is Information
			filterLoggerSettings.Add("Default", LogLevel.Information);
			// TODO: Probably should have a way to configure these as well
			filterLoggerSettings.Add("System", LogLevel.Warning);
			filterLoggerSettings.Add("Microsoft", LogLevel.Warning);
			if (!string.IsNullOrWhiteSpace(debugArgs))
			{
				if (debugArgs.Trim() == "1")
				{
					// Increase all logging to Trace
					filterLoggerSettings.Add("Stratis.Bitcoin", LogLevel.Trace);
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
							filterLoggerSettings.Add(category, LogLevel.Trace);
						}
						else
						{
							// Can directly specify something like -debug=Stratis.Bitcoin.Miner
							filterLoggerSettings.Add(key, LogLevel.Trace);
						}
					}
				}
			}

			// TODO: Additional args
			//var logipsArgs = args.Where(a => a.StartsWith("-logips=")).Select(a => a.Substring("-logips=".Length).Replace("\"", "")).FirstOrDefault();
			//var printtoconsoleArgs = args.Where(a => a.StartsWith("-printtoconsole=")).Select(a => a.Substring("-printtoconsole=".Length).Replace("\"", "")).FirstOrDefault();

			ILoggerFactory loggerFactory = new LoggerFactory()
				.WithFilter(filterLoggerSettings);
			loggerFactory.AddDebug(LogLevel.Trace);
			loggerFactory.AddConsole(LogLevel.Trace);
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

		public const int ColumnLength = 16;
	}
}

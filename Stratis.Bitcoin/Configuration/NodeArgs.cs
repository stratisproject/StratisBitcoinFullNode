using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;

namespace Stratis.Bitcoin.Configuration
{
	public class RPCArgs
	{
		public RPCArgs()
		{
			Bind = new List<IPEndPoint>();
			AllowIp = new List<IPAddress>();
		}

		public string RpcUser
		{
			get; set;
		}
		public string RpcPassword
		{
			get; set;
		}

		public int RPCPort
		{
			get; set;
		}
		public List<IPEndPoint> Bind
		{
			get; set;
		}

		public List<IPAddress> AllowIp
		{
			get; set;
		}

		public string[] GetUrls()
		{
			return Bind.Select(b => "http://" + b + "/").ToArray();
		}
	}

	public class NodeServerEndpoint
	{
		public NodeServerEndpoint()
		{

		}
		public NodeServerEndpoint(IPEndPoint endpoint, bool whitelisted)
		{
			Endpoint = endpoint;
			Whitelisted = whitelisted;
		}
		public IPEndPoint Endpoint
		{
			get; set;
		}
		public bool Whitelisted
		{
			get; set;
		}
	}
	public class ConnectionManagerArgs
	{
		public ConnectionManagerArgs()
		{
		}
		public List<IPEndPoint> Connect
		{
			get; set;
		} = new List<IPEndPoint>();
		public List<IPEndPoint> AddNode
		{
			get; set;
		} = new List<IPEndPoint>();
		public List<NodeServerEndpoint> Listen
		{
			get; set;
		} = new List<NodeServerEndpoint>();
		public IPEndPoint ExternalEndpoint
		{
			get;
			internal set;
		}
	}

	public class CacheArgs
	{
		public int MaxItems
		{
			get; set;
		} = 100000;
	}

	public class MempoolArgs
	{
		// Default for blocks only 
		const bool DEFAULT_BLOCKSONLY = false;
		// Default for DEFAULT_WHITELISTRELAY. 
		const bool DEFAULT_WHITELISTRELAY = true;

		public int MaxMempool { get; set; }
		public int MempoolExpiry { get; set; }
		public bool RelayPriority { get; set; }
		public int LimitFreeRelay { get; set; }
		public int LimitAncestors { get; set; }
		public int LimitAncestorSize { get; set; }
		public int LimitDescendants { get; set; }
		public int LimitDescendantSize { get; set; }
		public bool EnableReplacement { get; set; }
		public int MaxOrphanTx { get; set; }
		public bool RelayTxes { get; set; }
		public bool Whitelistrelay { get; set; }

		public void Load(TextFileConfiguration config)
		{
			this.MaxMempool = config.GetOrDefault("maxmempool", MempoolValidator.DefaultMaxMempoolSize);
			this.MempoolExpiry = config.GetOrDefault("mempoolexpiry", MempoolValidator.DefaultMempoolExpiry);
			this.RelayPriority = config.GetOrDefault("relaypriority", MempoolValidator.DefaultRelaypriority);
			this.LimitFreeRelay = config.GetOrDefault("limitfreerelay", MempoolValidator.DefaultLimitfreerelay);
			this.LimitAncestors = config.GetOrDefault("limitancestorcount", MempoolValidator.DefaultAncestorLimit);
			this.LimitAncestorSize = config.GetOrDefault("limitancestorsize", MempoolValidator.DefaultAncestorSizeLimit);
			this.LimitDescendants = config.GetOrDefault("limitdescendantcount", MempoolValidator.DefaultDescendantLimit);
			this.LimitDescendantSize = config.GetOrDefault("limitdescendantsize", MempoolValidator.DefaultDescendantSizeLimit);
			this.EnableReplacement = config.GetOrDefault("mempoolreplacement", MempoolValidator.DefaultEnableReplacement);
			this.MaxOrphanTx = config.GetOrDefault("maxorphantx", MempoolOrphans.DEFAULT_MAX_ORPHAN_TRANSACTIONS);
			this.RelayTxes = !config.GetOrDefault("blocksonly", DEFAULT_BLOCKSONLY);
			this.Whitelistrelay = config.GetOrDefault("whitelistrelay", DEFAULT_WHITELISTRELAY);
		}
	}

	public class NodeArgs
	{
		const int DEFAULT_MAX_TIP_AGE = 24 * 60 * 60;

		public RPCArgs RPC
		{
			get; set;
		}
		public CacheArgs Cache
		{
			get; set;
		} = new CacheArgs();
		public ConnectionManagerArgs ConnectionManager
		{
			get; set;
		} = new ConnectionManagerArgs();
		public MempoolArgs Mempool
		{
			get; set;
		} = new MempoolArgs();
		public bool Testnet
		{
			get; set;
		}
		public string DataDir
		{
			get; set;
		}
		public bool RegTest
		{
			get;
			set;
		}
		public string ConfigurationFile
		{
			get;
			set;
		}
		public bool Prune
		{
			get;
			set;
		}
		public bool RequireStandard
		{
			get;
			set;
		}
		public int MaxTipAge
		{
			get;
			set;
		}

		public static NodeArgs Default()
		{
			return NodeArgs.GetArgs(new string[0]);
		}

		public static NodeArgs GetArgs(string[] args)
		{
			NodeArgs nodeArgs = new NodeArgs();
			nodeArgs.ConfigurationFile = args.Where(a => a.StartsWith("-conf=")).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
			nodeArgs.DataDir = args.Where(a => a.StartsWith("-datadir=")).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
			if(nodeArgs.DataDir != null && nodeArgs.ConfigurationFile != null)
			{
				var isRelativePath = Path.GetFullPath(nodeArgs.ConfigurationFile).Length > nodeArgs.ConfigurationFile.Length;
				if(isRelativePath)
				{
					nodeArgs.ConfigurationFile = Path.Combine(nodeArgs.DataDir, nodeArgs.ConfigurationFile);
				}
			}
			nodeArgs.Testnet = args.Contains("-testnet", StringComparer.CurrentCultureIgnoreCase);
			nodeArgs.RegTest = args.Contains("-regtest", StringComparer.CurrentCultureIgnoreCase);

			if (nodeArgs.ConfigurationFile != null)
			{
				AssetConfigFileExists(nodeArgs);
				var configTemp = TextFileConfiguration.Parse(File.ReadAllText(nodeArgs.ConfigurationFile));
				nodeArgs.Testnet = configTemp.GetOrDefault<bool>("testnet", false);
				nodeArgs.RegTest = configTemp.GetOrDefault<bool>("regtest", false);
			}

			if (nodeArgs.Testnet && nodeArgs.RegTest)
				throw new ConfigurationException("Invalid combination of -regtest and -testnet");

			var network = nodeArgs.GetNetwork();
			if(nodeArgs.DataDir == null)
			{
				nodeArgs.DataDir = GetDefaultDataDir("stratisbitcoin", network);
			}

			if(nodeArgs.ConfigurationFile == null)
			{
				nodeArgs.ConfigurationFile = nodeArgs.GetDefaultConfigurationFile();
			}

			Logs.Configuration.LogInformation("Data directory set to " + nodeArgs.DataDir);
			Logs.Configuration.LogInformation("Configuration file set to " + nodeArgs.ConfigurationFile);

			if(!Directory.Exists(nodeArgs.DataDir))
				throw new ConfigurationException("Data directory does not exists");

			var consoleConfig = new TextFileConfiguration(args);
			var config = TextFileConfiguration.Parse(File.ReadAllText(nodeArgs.ConfigurationFile));
			consoleConfig.MergeInto(config);

			nodeArgs.Prune = config.GetOrDefault("prune", 0) != 0;
			nodeArgs.RequireStandard = config.GetOrDefault("acceptnonstdtxn", !(nodeArgs.RegTest || nodeArgs.Testnet));
			nodeArgs.MaxTipAge = config.GetOrDefault("maxtipage", DEFAULT_MAX_TIP_AGE);

			nodeArgs.RPC = config.GetOrDefault<bool>("server", false) ? new RPCArgs() : null;
			if(nodeArgs.RPC != null)
			{
				nodeArgs.RPC.RpcUser = config.GetOrDefault<string>("rpcuser", null);
				nodeArgs.RPC.RpcPassword = config.GetOrDefault<string>("rpcpassword", null);
				if(nodeArgs.RPC.RpcPassword == null && nodeArgs.RPC.RpcUser != null)
					throw new ConfigurationException("rpcpassword should be provided");
				if(nodeArgs.RPC.RpcUser == null && nodeArgs.RPC.RpcPassword != null)
					throw new ConfigurationException("rpcuser should be provided");

				var defaultPort = config.GetOrDefault<int>("rpcport", network.RPCPort);
				nodeArgs.RPC.RPCPort = defaultPort;
				try
				{
					nodeArgs.RPC.Bind = config
									.GetAll("rpcbind")
									.Select(p => ConvertToEndpoint(p, defaultPort))
									.ToList();
				}
				catch(FormatException)
				{
					throw new ConfigurationException("Invalid rpcbind value");
				}

				try
				{

					nodeArgs.RPC.AllowIp = config
									.GetAll("rpcallowip")
									.Select(p => IPAddress.Parse(p))
									.ToList();
				}
				catch(FormatException)
				{
					throw new ConfigurationException("Invalid rpcallowip value");
				}

				if(nodeArgs.RPC.AllowIp.Count == 0)
				{
					nodeArgs.RPC.Bind.Clear();
					nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::1"), defaultPort));
					nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), defaultPort));
					if(config.Contains("rpcbind"))
						Logs.Configuration.LogWarning("WARNING: option -rpcbind was ignored because -rpcallowip was not specified, refusing to allow everyone to connect");
				}

				if(nodeArgs.RPC.Bind.Count == 0)
				{
					nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::"), defaultPort));
					nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("0.0.0.0"), defaultPort));
				}
			}

			try
			{
				nodeArgs.ConnectionManager.Connect.AddRange(config.GetAll("connect")
					.Select(c => ConvertToEndpoint(c, network.DefaultPort)));
			}
			catch(FormatException)
			{
				throw new ConfigurationException("Invalid connect parameter");
			}

			try
			{
				nodeArgs.ConnectionManager.AddNode.AddRange(config.GetAll("addnode")
						.Select(c => ConvertToEndpoint(c, network.DefaultPort)));
			}
			catch(FormatException)
			{
				throw new ConfigurationException("Invalid addnode parameter");
			}

			var port = config.GetOrDefault<int>("port", network.DefaultPort);
			try
			{
				nodeArgs.ConnectionManager.Listen.AddRange(config.GetAll("listen")
						.Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), false)));
			}
			catch(FormatException)
			{
				throw new ConfigurationException("Invalid listen parameter");
			}

			try
			{
				nodeArgs.ConnectionManager.Listen.AddRange(config.GetAll("whitebind")
						.Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), true)));
			}
			catch(FormatException)
			{
				throw new ConfigurationException("Invalid listen parameter");
			}

			if(nodeArgs.ConnectionManager.Listen.Count == 0)
			{
				nodeArgs.ConnectionManager.Listen.Add(new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port), false));
			}

			var externalIp = config.GetOrDefault<string>("externalip", null);
			if(externalIp != null)
			{
				try
				{
					nodeArgs.ConnectionManager.ExternalEndpoint = ConvertToEndpoint(externalIp, port);
				}
				catch(FormatException)
				{
					throw new ConfigurationException("Invalid externalip parameter");
				}
			}

			if(nodeArgs.ConnectionManager.ExternalEndpoint == null)
			{
				nodeArgs.ConnectionManager.ExternalEndpoint = new IPEndPoint(IPAddress.Loopback, port);
			}

			nodeArgs.Mempool.Load(config);
				
			var folder = new DataFolder(nodeArgs.DataDir);
			if(!Directory.Exists(folder.CoinViewPath))
				Directory.CreateDirectory(folder.CoinViewPath);
			return nodeArgs;
		}

		private static void AssetConfigFileExists(NodeArgs nodeArgs)
		{
			if(!File.Exists(nodeArgs.ConfigurationFile))
				throw new ConfigurationException("Configuration file does not exists");
		}

		public static IPEndPoint ConvertToEndpoint(string str, int defaultPort)
		{
			var portOut = defaultPort;
			var hostOut = "";
			int colon = str.LastIndexOf(':');
			// if a : is found, and it either follows a [...], or no other : is in the string, treat it as port separator
			bool fHaveColon = colon != -1;
			bool fBracketed = fHaveColon && (str[0] == '[' && str[colon - 1] == ']'); // if there is a colon, and in[0]=='[', colon is not 0, so in[colon-1] is safe
			bool fMultiColon = fHaveColon && (str.LastIndexOf(':', colon - 1) != -1);
			if(fHaveColon && (colon == 0 || fBracketed || !fMultiColon))
			{
				int n;
				if(int.TryParse(str.Substring(colon + 1), out n) && n > 0 && n < 0x10000)
				{
					str = str.Substring(0, colon);
					portOut = n;
				}
			}
			if(str.Length > 0 && str[0] == '[' && str[str.Length - 1] == ']')
				hostOut = str.Substring(1, str.Length - 2);
			else
				hostOut = str;
			return new IPEndPoint(IPAddress.Parse(str), portOut);
		}

		private string GetDefaultConfigurationFile()
		{
			var config = Path.Combine(DataDir, "bitcoin.conf");
			Logs.Configuration.LogInformation("Configuration file set to " + config);
			if(!File.Exists(config))
			{
				Logs.Configuration.LogInformation("Creating configuration file");

				StringBuilder builder = new StringBuilder();
				builder.AppendLine("####RPC Settings####");
				builder.AppendLine("#Activate RPC Server (default: 0)");
				builder.AppendLine("#server=0");
				builder.AppendLine("#Where the RPC Server binds (default: 127.0.0.1 and ::1)");
				builder.AppendLine("#rpcbind=127.0.0.1");
				builder.AppendLine("#Ip address allowed to connect to RPC (default all: 0.0.0.0 and ::)");
				builder.AppendLine("#rpcallowedip=127.0.0.1");
				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}

		public Network GetNetwork()
		{
			return Testnet ? Network.TestNet :
				RegTest ? Network.RegTest :
				Network.Main;
		}

		private static string GetDefaultDataDir(string appName, Network network)
		{
			string directory = null;
			var home = Environment.GetEnvironmentVariable("HOME");
			if(!string.IsNullOrEmpty(home))
			{
				Logs.Configuration.LogInformation("Using HOME environment variable for initializing application data");
				directory = home;
				directory = Path.Combine(directory, "." + appName.ToLowerInvariant());
			}
			else
			{
				var localAppData = Environment.GetEnvironmentVariable("APPDATA");
				if(!string.IsNullOrEmpty(localAppData))
				{
					Logs.Configuration.LogInformation("Using APPDATA environment variable for initializing application data");
					directory = localAppData;
					directory = Path.Combine(directory, appName);
				}
				else
				{
					throw new DirectoryNotFoundException("Could not find suitable datadir");
				}
			}
			if(!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			directory = Path.Combine(directory, network.Name);
			if(!Directory.Exists(directory))
			{
				Logs.Configuration.LogInformation("Creating data directory");
				Directory.CreateDirectory(directory);
			}
			return directory;
		}
	}
}

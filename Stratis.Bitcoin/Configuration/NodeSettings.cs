using NBitcoin;
using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Configuration.Settings;

namespace Stratis.Bitcoin.Configuration
{
	public class NodeSettings
	{
		const int DEFAULT_MAX_TIP_AGE = 24 * 60 * 60;

		public RpcSettings RPC
		{
			get; set;
		}
		public CacheSettings Cache
		{
			get; set;
		} = new CacheSettings();
		public ConnectionManagerSettings ConnectionManager
		{
			get; set;
		} = new ConnectionManagerSettings();
		public MempoolSettings Mempool
		{
			get; set;
		} = new MempoolSettings();
		public StoreSettings Store
		{
			get; set;
		} = new StoreSettings();
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

		public static NodeSettings Default()
		{
			return NodeSettings.FromArguments(new string[0]);
		}

		public static NodeSettings FromArguments(string[] args)
		{
			NodeSettings nodeSettings = new NodeSettings();
			nodeSettings.ConfigurationFile = args.Where(a => a.StartsWith("-conf=")).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
			nodeSettings.DataDir = args.Where(a => a.StartsWith("-datadir=")).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
			if (nodeSettings.DataDir != null && nodeSettings.ConfigurationFile != null)
			{
				var isRelativePath = Path.GetFullPath(nodeSettings.ConfigurationFile).Length > nodeSettings.ConfigurationFile.Length;
				if (isRelativePath)
				{
					nodeSettings.ConfigurationFile = Path.Combine(nodeSettings.DataDir, nodeSettings.ConfigurationFile);
				}
			}
			nodeSettings.Testnet = args.Contains("-testnet", StringComparer.CurrentCultureIgnoreCase);
			nodeSettings.RegTest = args.Contains("-regtest", StringComparer.CurrentCultureIgnoreCase);

			if (nodeSettings.ConfigurationFile != null)
			{
				AssetConfigFileExists(nodeSettings);
				var configTemp = TextFileConfiguration.Parse(File.ReadAllText(nodeSettings.ConfigurationFile));
				nodeSettings.Testnet = configTemp.GetOrDefault<bool>("testnet", false);
				nodeSettings.RegTest = configTemp.GetOrDefault<bool>("regtest", false);
			}

			if (nodeSettings.Testnet && nodeSettings.RegTest)
				throw new ConfigurationException("Invalid combination of -regtest and -testnet");

			var network = nodeSettings.GetNetwork();
			if (nodeSettings.DataDir == null)
			{
				nodeSettings.DataDir = GetDefaultDataDir("stratisbitcoin", network);
			}

			if (nodeSettings.ConfigurationFile == null)
			{
				nodeSettings.ConfigurationFile = nodeSettings.GetDefaultConfigurationFile();
			}

			Logs.Configuration.LogInformation("Data directory set to " + nodeSettings.DataDir);
			Logs.Configuration.LogInformation("Configuration file set to " + nodeSettings.ConfigurationFile);

			if (!Directory.Exists(nodeSettings.DataDir))
				throw new ConfigurationException("Data directory does not exists");

			var consoleConfig = new TextFileConfiguration(args);
			var config = TextFileConfiguration.Parse(File.ReadAllText(nodeSettings.ConfigurationFile));
			consoleConfig.MergeInto(config);

			nodeSettings.RequireStandard = config.GetOrDefault("acceptnonstdtxn", !(nodeSettings.RegTest || nodeSettings.Testnet));
			nodeSettings.MaxTipAge = config.GetOrDefault("maxtipage", DEFAULT_MAX_TIP_AGE);

			nodeSettings.RPC = config.GetOrDefault<bool>("server", false) ? new RpcSettings() : null;
			if (nodeSettings.RPC != null)
			{
				nodeSettings.RPC.RpcUser = config.GetOrDefault<string>("rpcuser", null);
				nodeSettings.RPC.RpcPassword = config.GetOrDefault<string>("rpcpassword", null);
				if (nodeSettings.RPC.RpcPassword == null && nodeSettings.RPC.RpcUser != null)
					throw new ConfigurationException("rpcpassword should be provided");
				if (nodeSettings.RPC.RpcUser == null && nodeSettings.RPC.RpcPassword != null)
					throw new ConfigurationException("rpcuser should be provided");

				var defaultPort = config.GetOrDefault<int>("rpcport", network.RPCPort);
				nodeSettings.RPC.RPCPort = defaultPort;
				try
				{
					nodeSettings.RPC.Bind = config
									.GetAll("rpcbind")
									.Select(p => ConvertToEndpoint(p, defaultPort))
									.ToList();
				}
				catch (FormatException)
				{
					throw new ConfigurationException("Invalid rpcbind value");
				}

				try
				{

					nodeSettings.RPC.AllowIp = config
									.GetAll("rpcallowip")
									.Select(p => IPAddress.Parse(p))
									.ToList();
				}
				catch (FormatException)
				{
					throw new ConfigurationException("Invalid rpcallowip value");
				}

				if (nodeSettings.RPC.AllowIp.Count == 0)
				{
					nodeSettings.RPC.Bind.Clear();
					nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::1"), defaultPort));
					nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), defaultPort));
					if (config.Contains("rpcbind"))
						Logs.Configuration.LogWarning("WARNING: option -rpcbind was ignored because -rpcallowip was not specified, refusing to allow everyone to connect");
				}

				if (nodeSettings.RPC.Bind.Count == 0)
				{
					nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::"), defaultPort));
					nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("0.0.0.0"), defaultPort));
				}
			}

			try
			{
				nodeSettings.ConnectionManager.Connect.AddRange(config.GetAll("connect")
					.Select(c => ConvertToEndpoint(c, network.DefaultPort)));
			}
			catch (FormatException)
			{
				throw new ConfigurationException("Invalid connect parameter");
			}

			try
			{
				nodeSettings.ConnectionManager.AddNode.AddRange(config.GetAll("addnode")
						.Select(c => ConvertToEndpoint(c, network.DefaultPort)));
			}
			catch (FormatException)
			{
				throw new ConfigurationException("Invalid addnode parameter");
			}

			var port = config.GetOrDefault<int>("port", network.DefaultPort);
			try
			{
				nodeSettings.ConnectionManager.Listen.AddRange(config.GetAll("listen")
						.Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), false)));
			}
			catch (FormatException)
			{
				throw new ConfigurationException("Invalid listen parameter");
			}

			try
			{
				nodeSettings.ConnectionManager.Listen.AddRange(config.GetAll("whitebind")
						.Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), true)));
			}
			catch (FormatException)
			{
				throw new ConfigurationException("Invalid listen parameter");
			}

			if (nodeSettings.ConnectionManager.Listen.Count == 0)
			{
				nodeSettings.ConnectionManager.Listen.Add(new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port), false));
			}

			var externalIp = config.GetOrDefault<string>("externalip", null);
			if (externalIp != null)
			{
				try
				{
					nodeSettings.ConnectionManager.ExternalEndpoint = ConvertToEndpoint(externalIp, port);
				}
				catch (FormatException)
				{
					throw new ConfigurationException("Invalid externalip parameter");
				}
			}

			if (nodeSettings.ConnectionManager.ExternalEndpoint == null)
			{
				nodeSettings.ConnectionManager.ExternalEndpoint = new IPEndPoint(IPAddress.Loopback, network.DefaultPort);
			}

			nodeSettings.Mempool.Load(config);
			nodeSettings.Store.Load(config);

			var folder = new DataFolder(nodeSettings);
			if (!Directory.Exists(folder.CoinViewPath))
				Directory.CreateDirectory(folder.CoinViewPath);
			return nodeSettings;
		}

		private static void AssetConfigFileExists(NodeSettings nodeSettings)
		{
			if (!File.Exists(nodeSettings.ConfigurationFile))
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
			if (fHaveColon && (colon == 0 || fBracketed || !fMultiColon))
			{
				int n;
				if (int.TryParse(str.Substring(colon + 1), out n) && n > 0 && n < 0x10000)
				{
					str = str.Substring(0, colon);
					portOut = n;
				}
			}
			if (str.Length > 0 && str[0] == '[' && str[str.Length - 1] == ']')
				hostOut = str.Substring(1, str.Length - 2);
			else
				hostOut = str;
			return new IPEndPoint(IPAddress.Parse(str), portOut);
		}

		private string GetDefaultConfigurationFile()
		{
			var config = Path.Combine(DataDir, "bitcoin.conf");
			Logs.Configuration.LogInformation("Configuration file set to " + config);
			if (!File.Exists(config))
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
			if (!string.IsNullOrEmpty(home))
			{
				Logs.Configuration.LogInformation("Using HOME environment variable for initializing application data");
				directory = home;
				directory = Path.Combine(directory, "." + appName.ToLowerInvariant());
			}
			else
			{
				var localAppData = Environment.GetEnvironmentVariable("APPDATA");
				if (!string.IsNullOrEmpty(localAppData))
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
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			directory = Path.Combine(directory, network.Name);
			if (!Directory.Exists(directory))
			{
				Logs.Configuration.LogInformation("Creating data directory");
				Directory.CreateDirectory(directory);
			}
			return directory;
		}
	}
}

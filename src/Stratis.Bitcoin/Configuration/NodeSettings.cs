using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NLog.Extensions.Logging;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Configuration
{
    internal static class NormalizeDirectorySeparatorExt
    {
        /// <summary>
        /// Fixes incorrect directory separator characters in path (if any)
        /// </summary>
        public static string NormalizeDirectorySeparator(this string path)
        {
            // Replace incorrect with correct
            return path.Replace((Path.DirectorySeparatorChar == '/') ? '\\' : '/', Path.DirectorySeparatorChar);
        }
    }

    /// <summary>
    /// Node configuration complied from both the application command line arguments and the configuration file.
    /// </summary>
    public class NodeSettings
    {
        /// <summary>Version of the protocol the current implementation supports.</summary>
        public const ProtocolVersion SupportedProtocolVersion = ProtocolVersion.SENDHEADERS_VERSION;

        /// <summary>Default value for Maximum tip age in seconds to consider node in initial block download.</summary>
        public const int DefaultMaxTipAge = 24 * 60 * 60;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="name">Blockchain name. Currently only "bitcoin" and "stratis" are used.</param>
        /// <param name="innerNetwork">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="protocolVersion">Supported protocol version for which to create the configuration.</param>
        /// <param name="agent">The nodes user agent that will be shared with peers.</param>
        public NodeSettings(string name = "bitcoin", Network innerNetwork = null, ProtocolVersion protocolVersion = SupportedProtocolVersion, string agent = "StratisBitcoin")
        {
            if (string.IsNullOrEmpty(name))
                throw new ConfigurationException("A network name is mandatory.");

            this.Name = name;
            this.Agent = agent;
            this.Network = innerNetwork;
            this.ProtocolVersion = protocolVersion;

            this.ConnectionManager = new ConnectionManagerSettings();
            this.Log = new LogSettings();
            this.LoggerFactory = new ExtendedLoggerFactory();
            this.LoggerFactory.AddConsoleWithFilters();
            this.LoggerFactory.AddNLog();
            this.Logger = this.LoggerFactory.CreateLogger(typeof(NodeSettings).FullName);
        }

        /// <summary>Factory to create instance logger.</summary>
        public ILoggerFactory LoggerFactory { get; }

        /// <summary>Instance logger.</summary>
        public ILogger Logger { get; private set; }

        /// <summary>Configuration related to incoming and outgoing connections.</summary>
        public ConnectionManagerSettings ConnectionManager { get; set; }

        /// <summary>Configuration related to logging.</summary>
        public LogSettings Log { get; set; }

        /// <summary>List of paths to important files and folders.</summary>
        public DataFolder DataFolder { get; set; }

        /// <summary>Path to the data directory.</summary>
        public string DataDir { get; set; }

        /// <summary><c>true</c> if the node should run on testnet.</summary>
        public bool Testnet { get; set; }

        /// <summary><c>true</c> if the node should run in regtest mode.</summary>
        public bool RegTest { get; set; }

        /// <summary>Path to the configuration file.</summary>
        public string ConfigurationFile { get; set; }

        /// <summary>Option to skip (most) non-standard transaction checks, for testnet/regtest only.</summary>
        public bool RequireStandard { get; set; }

        /// <summary>Maximum tip age in seconds to consider node in initial block download.</summary>
        public int MaxTipAge { get; set; }

        /// <summary>Supported protocol version.</summary>
        public ProtocolVersion ProtocolVersion { get; set; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        /// <summary>Blockchain name. Currently only "bitcoin" and "stratis" are used.</summary>
        public string Name { get; set; }

        /// <summary>The node's user agent that will be shared with peers in the version handshake.</summary>
        public string Agent { get; set; }

        /// <summary>URI to node's API interface.</summary>
        public Uri ApiUri { get; set; }

        /// <summary>Minimum transaction fee for network.</summary>
        public FeeRate MinTxFeeRate { get; set; }

        /// <summary>Fall back transaction fee for network.</summary>
        public FeeRate FallbackTxFeeRate { get; set; }

        /// <summary>Minimum relay transaction fee for network.</summary>
        public FeeRate MinRelayTxFeeRate { get; set; }

        public TextFileConfiguration ConfigReader { get; private set; }

        /// <summary><c>true</c> to sync time with other peers and calculate adjusted time, <c>false</c> to use our system clock only.</summary>
        public bool SyncTimeEnabled { get; set; }

        /// <summary>
        /// Initializes default configuration.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="protocolVersion">Supported protocol version for which to create the configuration.</param>
        /// <returns>Default node configuration.</returns>
        public static NodeSettings Default(Network network = null, ProtocolVersion protocolVersion = SupportedProtocolVersion)
        {
            NodeSettings nodeSettings = new NodeSettings(innerNetwork: network);
            nodeSettings.LoadArguments(new string[0]);
            return nodeSettings;
        }

        /// <summary>
        /// Initializes configuration from command line arguments.
        /// <para>This includes loading configuration from file.</para>
        /// </summary>
        /// <param name="args">Application command line arguments.</param>
        /// <returns>Initialized node configuration.</returns>
        /// <exception cref="ConfigurationException">Thrown in case of any problems with the configuration file or command line arguments.</exception>
        public NodeSettings LoadArguments(string[] args)
        {
            // By default, we look for a file named '<network>.conf' in the network's data directory,
            // but both the data directory and the configuration file path may be changed using the -datadir and -conf command-line arguments.
            this.ConfigurationFile = args.GetValueOf("-conf")?.NormalizeDirectorySeparator();
            this.DataDir = args.GetValueOf("-datadir")?.NormalizeDirectorySeparator();

            // If the configuration file is relative then assume it is relative to the data folder and combine the paths
            if (this.DataDir != null && this.ConfigurationFile != null)
            {
                bool isRelativePath = Path.GetFullPath(this.ConfigurationFile).Length > this.ConfigurationFile.Length;
                if (isRelativePath)
                    this.ConfigurationFile = Path.Combine(this.DataDir, this.ConfigurationFile);
            }

            // Find out if we need to run on testnet or regtest from the config file.
            if (this.ConfigurationFile != null)
            {
                AssertConfigFileExists(this.ConfigurationFile);
                var configTemp = new TextFileConfiguration(File.ReadAllText(this.ConfigurationFile));
                this.Testnet = configTemp.GetOrDefault<bool>("testnet", false);
                this.RegTest = configTemp.GetOrDefault<bool>("regtest", false);
            }

            //Only if args contains -testnet, do we set it to true, otherwise it overwrites file configuration
            if (args.Contains("-testnet", StringComparer.CurrentCultureIgnoreCase))
                this.Testnet = true;

            //Only if args contains -regtest, do we set it to true, otherwise it overwrites file configuration
            if (args.Contains("-regtest", StringComparer.CurrentCultureIgnoreCase))
                this.RegTest = true;

            if (this.Testnet && this.RegTest)
                throw new ConfigurationException("Invalid combination of -regtest and -testnet.");

            this.Network = this.GetNetwork();
            if (this.DataDir == null)
            {
                this.DataDir = this.CreateDefaultDataDirectories(Path.Combine("StratisNode", this.Name), this.Network);
            }

            if (!Directory.Exists(this.DataDir))
                throw new ConfigurationException($"Data directory {this.DataDir} does not exist.");

            // If no configuration file path is passed in the args, load the default file.
            if (this.ConfigurationFile == null)
            {
                this.ConfigurationFile = this.CreateDefaultConfigurationFile();
            }

            var consoleConfig = new TextFileConfiguration(args);
            var config = new TextFileConfiguration(File.ReadAllText(this.ConfigurationFile));
            this.ConfigReader = config;
            consoleConfig.MergeInto(config);

            this.DataFolder = new DataFolder(this);
            if (!Directory.Exists(this.DataFolder.CoinViewPath))
                Directory.CreateDirectory(this.DataFolder.CoinViewPath);

            // Set the configuration filter and file path.
            this.Log.Load(config);
            this.LoggerFactory.AddFilters(this.Log, this.DataFolder);
            this.LoggerFactory.ConfigureConsoleFilters(this.LoggerFactory.GetConsoleSettings(), this.Log);

            this.Logger.LogDebug("Data directory set to '{0}'.", this.DataDir);
            this.Logger.LogDebug("Configuration file set to '{0}'.", this.ConfigurationFile);

            this.RequireStandard = config.GetOrDefault("acceptnonstdtxn", !(this.RegTest || this.Testnet));
            this.MaxTipAge = config.GetOrDefault("maxtipage", DefaultMaxTipAge);
            this.ApiUri = config.GetOrDefault("apiuri", (this.Network == Network.StratisMain || this.Network == Network.StratisTest || this.Network == Network.StratisRegTest) ? new Uri("http://localhost:37221") : new Uri("http://localhost:37220"));

            this.Logger.LogDebug("Network: IsTest='{0}', IsBitcoin='{1}'.", this.Network.IsTest(), this.Network.IsBitcoin());
            this.MinTxFeeRate = new FeeRate(config.GetOrDefault("mintxfee", this.Network.MinTxFee));
            this.Logger.LogDebug("MinTxFeeRate set to {0}.", this.MinTxFeeRate);
            this.FallbackTxFeeRate = new FeeRate(config.GetOrDefault("fallbackfee", this.Network.FallbackFee));
            this.Logger.LogDebug("FallbackTxFeeRate set to {0}.", this.FallbackTxFeeRate);
            this.MinRelayTxFeeRate = new FeeRate(config.GetOrDefault("minrelaytxfee", this.Network.MinRelayTxFee));
            this.Logger.LogDebug("MinRelayTxFeeRate set to {0}.", this.MinRelayTxFeeRate);

            this.SyncTimeEnabled = config.GetOrDefault<bool>("synctime", true);
            this.Logger.LogDebug("Time synchronization with peers is {0}.", this.SyncTimeEnabled ? "enabled" : "disabled");

            try
            {
                this.ConnectionManager.Connect.AddRange(config.GetAll("connect")
                    .Select(c => ConvertIpAddressToEndpoint(c, this.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'connect' parameter.");
            }

            try
            {
                this.ConnectionManager.AddNode.AddRange(config.GetAll("addnode")
                        .Select(c => ConvertIpAddressToEndpoint(c, this.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'addnode' parameter.");
            }

            var port = config.GetOrDefault<int>("port", this.Network.DefaultPort);
            try
            {
                this.ConnectionManager.Listen.AddRange(config.GetAll("bind")
                        .Select(c => new NodeServerEndpoint(ConvertIpAddressToEndpoint(c, port), false)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'bind' parameter");
            }

            try
            {
                this.ConnectionManager.Listen.AddRange(config.GetAll("whitebind")
                        .Select(c => new NodeServerEndpoint(ConvertIpAddressToEndpoint(c, port), true)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'listen' parameter");
            }

            if (this.ConnectionManager.Listen.Count == 0)
            {
                this.ConnectionManager.Listen.Add(new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port), false));
            }

            var externalIp = config.GetOrDefault<string>("externalip", null);
            if (externalIp != null)
            {
                try
                {
                    this.ConnectionManager.ExternalEndpoint = ConvertIpAddressToEndpoint(externalIp, port);
                }
                catch (FormatException)
                {
                    throw new ConfigurationException("Invalid 'externalip' parameter");
                }
            }

            if (this.ConnectionManager.ExternalEndpoint == null)
            {
                this.ConnectionManager.ExternalEndpoint = new IPEndPoint(IPAddress.Loopback, this.Network.DefaultPort);
            }

            this.ConnectionManager.BanTimeSeconds = config.GetOrDefault<int>("bantime", ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds);
            return this;
        }

        /// <summary>
        /// Asserts the configuration file exists.
        /// </summary>
        /// <param name="configurationFilePath">The configuration file path.</param>
        /// <exception cref="ConfigurationException">Thrown if the configuration file does not exist.</exception>
        private static void AssertConfigFileExists(string configurationFilePath)
        {
            if (!File.Exists(configurationFilePath))
                throw new ConfigurationException($"Configuration file does not exist at {configurationFilePath}.");
        }

        /// <summary>
        /// Converts a string to an IP endpoint.
        /// </summary>
        /// <param name="ipAddress">String to convert.</param>
        /// <param name="port">Port to use if <paramref name="ipAddress"/> does not specify it.</param>
        /// <returns>IP end point representation of the string.</returns>
        /// <remarks>
        /// IP addresses can have a port specified such that the format of <paramref name="ipAddress"/> is as such: address:port.
        /// IPv4 and IPv6 addresses are supported.
        /// In the case where the default port is passed and the IP address has a port specified in it, the IP address's port will take precedence.
        /// Examples of addresses that are supported are: 15.61.23.23, 15.61.23.23:1500, [1233:3432:2434:2343:3234:2345:6546:4534], [1233:3432:2434:2343:3234:2345:6546:4534]:8333.</remarks>
        public static IPEndPoint ConvertIpAddressToEndpoint(string ipAddress, int port)
        {
            // Checks the validity of the parameters passed.
            Guard.NotEmpty(ipAddress, nameof(ipAddress));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new ConfigurationException($"Port {port} was outside of the values that can assigned for a port [{IPEndPoint.MinPort}-{IPEndPoint.MaxPort}].");
            }

            int colon = ipAddress.LastIndexOf(':');

            // if a : is found, and it either follows a [...], or no other : is in the string, treat it as port separator
            bool fHaveColon = colon != -1;
            bool fBracketed = fHaveColon && (ipAddress[0] == '[' && ipAddress[colon - 1] == ']'); // if there is a colon, and in[0]=='[', colon is not 0, so in[colon-1] is safe
            bool fMultiColon = fHaveColon && (ipAddress.LastIndexOf(':', colon - 1) != -1);
            if (fHaveColon && (colon == 0 || fBracketed || !fMultiColon))
            {
                if (int.TryParse(ipAddress.Substring(colon + 1), out var n) && n > IPEndPoint.MinPort && n < IPEndPoint.MaxPort)
                {
                    ipAddress = ipAddress.Substring(0, colon);
                    port = n;
                }
            }

            return new IPEndPoint(IPAddress.Parse(ipAddress), port);
        }

        /// <summary>
        /// Creates a default configuration file if no configuration file is found.
        /// </summary>
        /// <returns>Path to the configuration file.</returns>
        private string CreateDefaultConfigurationFile()
        {
            string configFilePath = Path.Combine(this.DataDir, $"{this.Name}.conf");
            this.Logger.LogDebug("Configuration file set to '{0}'.", configFilePath);

            // Create a config file if none exist.
            if (!File.Exists(configFilePath))
            {
                this.Logger.LogDebug("Creating configuration file...");

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("####RPC Settings####");
                builder.AppendLine("#Activate RPC Server (default: 0)");
                builder.AppendLine("#server=0");
                builder.AppendLine("#Where the RPC Server binds (default: 127.0.0.1 and ::1)");
                builder.AppendLine("#rpcbind=127.0.0.1");
                builder.AppendLine("#Ip address allowed to connect to RPC (default all: 0.0.0.0 and ::)");
                builder.AppendLine("#rpcallowip=127.0.0.1");
                File.WriteAllText(configFilePath, builder.ToString());
            }
            return configFilePath;
        }

        /// <summary>
        /// Obtains the network to run on using the current settings.
        /// </summary>
        /// <returns>Specification of the network.</returns>
        public Network GetNetwork()
        {
            if (this.Network != null)
                return this.Network;

            return this.Testnet ? Network.TestNet :
                this.RegTest ? Network.RegTest :
                Network.Main;
        }

        /// <summary>
        /// Creates default data directories respecting different operating system specifics.
        /// </summary>
        /// <param name="appName">Name of the node, which will be reflected in the name of the data directory.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <returns>The top-level data directory path.</returns>
        private string CreateDefaultDataDirectories(string appName, Network network)
        {
            string directoryPath;

            // Directory paths are different between Windows or Linux/OSX systems.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrEmpty(home))
                {
                    this.Logger.LogDebug("Using HOME environment variable for initializing application data.");
                    directoryPath = Path.Combine(home, "." + appName.ToLowerInvariant());
                }
                else
                {
                    throw new DirectoryNotFoundException("Could not find HOME directory.");
                }
            }
            else
            {
                var localAppData = Environment.GetEnvironmentVariable("APPDATA");
                if (!string.IsNullOrEmpty(localAppData))
                {
                    this.Logger.LogDebug("Using APPDATA environment variable for initializing application data.");
                    directoryPath = Path.Combine(localAppData, appName);
                }
                else
                {
                    throw new DirectoryNotFoundException("Could not find APPDATA directory.");
                }
            }

            // Create the data directories if they don't exist.
            Directory.CreateDirectory(directoryPath);
            directoryPath = Path.Combine(directoryPath, network.Name);
            Directory.CreateDirectory(directoryPath);

            this.Logger.LogDebug("Data directory initialized with path {0}.", directoryPath);
            return directoryPath;
        }

        /// <summary>
        /// Checks whether to show a help and possibly shows the help.
        /// </summary>
        /// <param name="args">Application command line arguments.</param>
        /// <param name="mainNet">Main network description to extract port values from.</param>
        /// <returns><c>true</c> if the help was displayed, <c>false</c> otherwise.</returns>
        public static bool PrintHelp(string[] args, Network mainNet)
        {
            Guard.NotNull(mainNet, nameof(mainNet));

            if (args != null && args.Length == 1 && (args[0].StartsWith("-help") || args[0].StartsWith("--help")))
            {
                var defaults = Default();

                var builder = new StringBuilder();
                builder.AppendLine("Usage:");
                // TODO: Shouldn't this be dotnet run instead of dotnet exec?
                builder.AppendLine(" dotnet exec <Stratis.StratisD/BitcoinD.dll> [arguments]");
                builder.AppendLine();
                builder.AppendLine("Command line arguments:");
                builder.AppendLine();
                builder.AppendLine($"-help/--help              Show this help.");
                builder.AppendLine($"-conf=<Path>              Path to the configuration file. Default {defaults.ConfigurationFile}.");
                builder.AppendLine($"-datadir=<Path>           Path to the data directory. Default {defaults.DataDir}.");
                builder.AppendLine($"-testnet                  Use the testnet chain.");
                builder.AppendLine($"-regtest                  Use the regtestnet chain.");
                builder.AppendLine($"-acceptnonstdtxn=<0 or 1> Accept non-standard transactions. Default {defaults.RequireStandard}.");
                builder.AppendLine($"-maxtipage=<number>       Max tip age. Default {DefaultMaxTipAge}.");
                builder.AppendLine($"-connect=<ip:port>        Specified node to connect to. Can be specified multiple times.");
                builder.AppendLine($"-addnode=<ip:port>        Add a node to connect to and attempt to keep the connection open. Can be specified multiple times.");
                builder.AppendLine($"-whitebind=<ip:port>      Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6. Can be specified multiple times.");
                builder.AppendLine($"-externalip=<ip>          Specify your own public address.");
                builder.AppendLine($"-synctime=<0 or 1>        Sync with peers. Default 1.");
                builder.AppendLine($"-checkpoints=<0 or 1>     Use checkpoints. Default 1.");
                builder.AppendLine($"-mintxfee=<number>        Minimum fee rate. Defaults to network specific value.");
                builder.AppendLine($"-fallbackfee=<number>     Fallback fee rate. Defaults to network specific value.");
                builder.AppendLine($"-minrelaytxfee=<number>   Minimum relay fee rate. Defaults to network specific value.");
                builder.AppendLine($"-bantime=<number>         Number of seconds to keep misbehaving peers from reconnecting (Default 24-hour ban).");
                builder.AppendLine($"-assumevalid=<hex>        If this block is in the chain assume that it and its ancestors are valid and potentially skip their script verification(0 to verify all). Defaults to network specific value.");

                defaults.Logger.LogInformation(builder.ToString());

                return true;
            }

            return false;
        }
    }
}

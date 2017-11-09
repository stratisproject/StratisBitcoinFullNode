using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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

        /// <summary>Whether use of checkpoints is enabled or not.</summary>
        public bool UseCheckpoints { get; set; }

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
            // The logger factory goes in the settings with minimal configuration, 
            // that's so the settings can also log out its progress.
            this.LoggerFactory.AddConsoleWithFilters(out ConsoleLoggerSettings consoleSettings);
            this.LoggerFactory.AddNLog();
            this.Logger = this.LoggerFactory.CreateLogger(typeof(NodeSettings).FullName);

            
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
                AssertConfigFileExists(this);
                var configTemp = new TextFileConfiguration(File.ReadAllText(this.ConfigurationFile));
                this.Testnet = configTemp.GetOrDefault<bool>("testnet", false);
                this.RegTest = configTemp.GetOrDefault<bool>("regtest", false);
            }

            this.Testnet = args.Contains("-testnet", StringComparer.CurrentCultureIgnoreCase);
            this.RegTest = args.Contains("-regtest", StringComparer.CurrentCultureIgnoreCase);

            if (this.Testnet && this.RegTest)
                throw new ConfigurationException("Invalid combination of -regtest and -testnet.");
            
            this.Network = this.GetNetwork();
            if (this.DataDir == null)
            {
                this.SetDefaultDataDir(Path.Combine("StratisNode", this.Name), this.Network);
            }

            if (!Directory.Exists(this.DataDir))
                throw new ConfigurationException($"Data directory {this.DataDir} does not exist.");

            if (this.ConfigurationFile == null)
            {
                this.ConfigurationFile = this.GetDefaultConfigurationFile();
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

            this.Logger.LogInformation("Data directory set to '{0}'.", this.DataDir);
            this.Logger.LogInformation("Configuration file set to '{0}'.", this.ConfigurationFile);

            this.RequireStandard = config.GetOrDefault("acceptnonstdtxn", !(this.RegTest || this.Testnet));
            this.MaxTipAge = config.GetOrDefault("maxtipage", DefaultMaxTipAge);
            this.ApiUri = config.GetOrDefault("apiuri", new Uri("http://localhost:37220"));

            this.Logger.LogDebug("Network: IsTest='{0}', IsBitcoin='{1}'.", this.Network.IsTest(), this.Network.IsBitcoin());
            this.MinTxFeeRate = new FeeRate(config.GetOrDefault("mintxfee", this.Network.MinTxFee));
            this.Logger.LogDebug("MinTxFeeRate set to {0}.", this.MinTxFeeRate);
            this.FallbackTxFeeRate = new FeeRate(config.GetOrDefault("fallbackfee", this.Network.FallbackFee));
            this.Logger.LogDebug("FallbackTxFeeRate set to {0}.", this.FallbackTxFeeRate);
            this.MinRelayTxFeeRate = new FeeRate(config.GetOrDefault("minrelaytxfee", this.Network.MinRelayTxFee));
            this.Logger.LogDebug("MinRelayTxFeeRate set to {0}.", this.MinRelayTxFeeRate);

            this.SyncTimeEnabled = config.GetOrDefault<bool>("synctime", true);
            this.Logger.LogDebug("Time synchronization with peers is {0}.", this.SyncTimeEnabled ? "enabled" : "disabled");

            this.UseCheckpoints = config.GetOrDefault<bool>("checkpoints", true);
            this.Logger.LogDebug("Checkpoints are {0}.", this.UseCheckpoints ? "enabled" : "disabled");

            try
            {
                this.ConnectionManager.Connect.AddRange(config.GetAll("connect")
                    .Select(c => ConvertToEndpoint(c, this.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'connect' parameter.");
            }

            try
            {
                this.ConnectionManager.AddNode.AddRange(config.GetAll("addnode")
                        .Select(c => ConvertToEndpoint(c, this.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'addnode' parameter.");
            }

            var port = config.GetOrDefault<int>("port", this.Network.DefaultPort);
            try
            {
                this.ConnectionManager.Listen.AddRange(config.GetAll("bind")
                        .Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), false)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'bind' parameter");
            }

            try
            {
                this.ConnectionManager.Listen.AddRange(config.GetAll("whitebind")
                        .Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), true)));
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
                    this.ConnectionManager.ExternalEndpoint = ConvertToEndpoint(externalIp, port);
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

            return this;
        }

        /// <summary>
        /// Asserts the configuration file exists.
        /// </summary>
        /// <param name="nodeSettings">Node configuration containing information about the configuration file path.</param>
        /// <exception cref="ConfigurationException">Thrown if the configuration file does not exist.</exception>
        private static void AssertConfigFileExists(NodeSettings nodeSettings)
        {
            if (!File.Exists(nodeSettings.ConfigurationFile))
                throw new ConfigurationException("Configuration file does not exist.");
        }

        /// <summary>
        /// Converts string to IP end point.
        /// </summary>
        /// <param name="str">String to convert.</param>
        /// <param name="defaultPort">Port to use if <paramref name="str"/> does not specify it.</param>
        /// <returns>IP end point representation of the string.</returns>
        public static IPEndPoint ConvertToEndpoint(string str, int defaultPort)
        {
            int portOut = defaultPort;
            string hostOut = "";
            int colon = str.LastIndexOf(':');
            // if a : is found, and it either follows a [...], or no other : is in the string, treat it as port separator
            bool fHaveColon = colon != -1;
            bool fBracketed = fHaveColon && (str[0] == '[' && str[colon - 1] == ']'); // if there is a colon, and in[0]=='[', colon is not 0, so in[colon-1] is safe
            bool fMultiColon = fHaveColon && (str.LastIndexOf(':', colon - 1) != -1);
            if (fHaveColon && (colon == 0 || fBracketed || !fMultiColon))
            {
                if (int.TryParse(str.Substring(colon + 1), out var n) && n > 0 && n < 0x10000)
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

        /// <summary>
        /// Creates a default configuration file if no configuration file is found.
        /// </summary>
        /// <returns>Path to the configuration file.</returns>
        private string GetDefaultConfigurationFile()
        {
            string config = Path.Combine(this.DataDir, $"{this.Name}.conf");
            this.Logger.LogInformation("Configuration file set to '{0}'.", config);
            if (!File.Exists(config))
            {
                this.Logger.LogInformation("Creating configuration file...");

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("####RPC Settings####");
                builder.AppendLine("#Activate RPC Server (default: 0)");
                builder.AppendLine("#server=0");
                builder.AppendLine("#Where the RPC Server binds (default: 127.0.0.1 and ::1)");
                builder.AppendLine("#rpcbind=127.0.0.1");
                builder.AppendLine("#Ip address allowed to connect to RPC (default all: 0.0.0.0 and ::)");
                builder.AppendLine("#rpcallowip=127.0.0.1");
                File.WriteAllText(config, builder.ToString());
            }
            return config;
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
        /// Finds a location of the default data directory respecting different operating system specifics.
        /// </summary>
        /// <param name="appName">Name of the node, which will be reflected in the name of the data directory.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        private void SetDefaultDataDir(string appName, Network network)
        {
            string directory = null;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrEmpty(home))
                {
                    this.Logger.LogInformation("Using HOME environment variable for initializing application data.");
                    directory = Path.Combine(home, "." + appName.ToLowerInvariant());
                }
                else
                {
                    throw new DirectoryNotFoundException("Could not find suitable datadir.");
                }
            }
            else
            {
                var localAppData = Environment.GetEnvironmentVariable("APPDATA");
                if (!string.IsNullOrEmpty(localAppData))
                {
                    this.Logger.LogInformation("Using APPDATA environment variable for initializing application data.");
                    directory = Path.Combine(localAppData, appName);
                }
                else
                {
                    throw new DirectoryNotFoundException("Could not find suitable datadir.");
                }
            }

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            directory = Path.Combine(directory, network.Name);
            if (!Directory.Exists(directory))
            {
                this.Logger.LogInformation("Creating data directory...");
                Directory.CreateDirectory(directory);
            }

            this.DataDir = directory;
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
                var defaults = NodeSettings.Default();

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

                defaults.Logger.LogInformation(builder.ToString());

                return true;
            }

            return false;
        }
    }
}
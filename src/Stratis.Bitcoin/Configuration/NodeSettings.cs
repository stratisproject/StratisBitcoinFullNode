using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NLog.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;
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

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="innerNetwork">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="protocolVersion">Supported protocol version for which to create the configuration.</param>
        /// <param name="agent">The nodes user agent that will be shared with peers.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="loadConfiguration">Determines whether to load the configuration file.</param>
        public NodeSettings(Network innerNetwork = null, ProtocolVersion protocolVersion = SupportedProtocolVersion, 
            string agent = "StratisBitcoin", string[] args = null, bool loadConfiguration = true)
        {
            this.Agent = agent;
            this.Network = innerNetwork;
            this.ProtocolVersion = protocolVersion;

            this.Log = new LogSettings();
            this.LoggerFactory = new ExtendedLoggerFactory();
            this.LoggerFactory.AddConsoleWithFilters();
            this.LoggerFactory.AddNLog();
            this.Logger = this.LoggerFactory.CreateLogger(typeof(NodeSettings).FullName);

            // Load arguments or configuration from .ctor?
            this.LoadArgs = args ?? new string[] { };

            // By default, we look for a file named '<network>.conf' in the network's data directory,
            // but both the data directory and the configuration file path may be changed using the -datadir and -conf command-line arguments.
            this.ConfigurationFile = this.LoadArgs.GetValueOf("-conf")?.NormalizeDirectorySeparator();
            this.DataDir = this.LoadArgs.GetValueOf("-datadir")?.NormalizeDirectorySeparator();

            // If the configuration file is relative then assume it is relative to the data folder and combine the paths
            if (this.DataDir != null && this.ConfigurationFile != null)
            {
                bool isRelativePath = Path.GetFullPath(this.ConfigurationFile).Length > this.ConfigurationFile.Length;
                if (isRelativePath)
                    this.ConfigurationFile = Path.Combine(this.DataDir, this.ConfigurationFile);
            }

            // If the network is not known then derive it from the command line arguments
            if (this.Network == null)
            {
                var regTest = false;
                var testNet = false;

                // Find out if we need to run on testnet or regtest from the config file.
                if (this.ConfigurationFile != null)
                {
                    AssertConfigFileExists(this.ConfigurationFile);
                    var configTemp = new TextFileConfiguration(File.ReadAllText(this.ConfigurationFile));
                    testNet = configTemp.GetOrDefault<bool>("testnet", false);
                    regTest = configTemp.GetOrDefault<bool>("regtest", false);
                }

                // Only if args contains -testnet, do we set it to true, otherwise it overwrites file configuration
                if (this.LoadArgs.Contains("-testnet", StringComparer.CurrentCultureIgnoreCase))
                    testNet = true;

                // Only if args contains -regtest, do we set it to true, otherwise it overwrites file configuration
                if (this.LoadArgs.Contains("-regtest", StringComparer.CurrentCultureIgnoreCase))
                    regTest = true;

                if (testNet && regTest)
                    throw new ConfigurationException("Invalid combination of -regtest and -testnet.");

                if (protocolVersion == ProtocolVersion.ALT_PROTOCOL_VERSION)
                    this.Network = testNet ? Network.StratisTest : regTest ? Network.StratisRegTest : Network.StratisMain;
                else
                    this.Network = testNet ? Network.TestNet : regTest ? Network.RegTest : Network.Main;
            }

            // Setting the data directory.
            if (this.DataDir == null)
            {
                this.DataDir = this.CreateDefaultDataDirectories(Path.Combine("StratisNode", this.Network.RootFolderName), this.Network);
            }
            else
            {
                // Create the data directories if they don't exist.
                string directoryPath = Path.Combine(this.DataDir, this.Network.RootFolderName, this.Network.Name);
                this.DataDir = Directory.CreateDirectory(directoryPath).FullName;
                this.Logger.LogDebug("Data directory initialized with path {0}.", this.DataDir);
            }

            this.DataFolder = new DataFolder(this.DataDir);

            // Load configuration from .ctor?
            if (loadConfiguration)
                this.LoadConfiguration();
        }

        /// <summary>Factory to create instance logger.</summary>
        public ILoggerFactory LoggerFactory { get; }

        /// <summary>Arguments to load.</summary>
        public string[] LoadArgs { get; private set;  }

        /// <summary>Instance logger.</summary>
        public ILogger Logger { get; private set; }

        /// <summary>Configuration related to logging.</summary>
        public LogSettings Log { get; set; }

        /// <summary>List of paths to important files and folders.</summary>
        public DataFolder DataFolder { get; set; }

        /// <summary>Path to the data directory. This value is read-only and is set in the constructor's args.</summary>
        public string DataDir { get; private set; }

        /// <summary>Path to the configuration file. This value is read-only and is set in the constructor's args.</summary>
        public string ConfigurationFile { get; private set; }

        /// <summary>Option to skip (most) non-standard transaction checks, for testnet/regtest only.</summary>
        public bool RequireStandard { get; set; }

        /// <summary>Determines whether to print help and exit.</summary>
        public bool PrintHelpAndExit
        {
            get
            {
                var args = this.LoadArgs;

                return args != null && args.Length == 1 && (args[0].StartsWith("-help") || args[0].StartsWith("--help"));
            }
        }

        /// <summary>Maximum tip age in seconds to consider node in initial block download.</summary>
        public int MaxTipAge { get; set; }

        /// <summary>Supported protocol version.</summary>
        public ProtocolVersion ProtocolVersion { get; set; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        /// <summary>The node's user agent that will be shared with peers in the version handshake.</summary>
        public string Agent { get; set; }

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
            return new NodeSettings(network, protocolVersion, args:new string[0]);
        }

        /// <summary>
        /// Loads the configuration file.
        /// </summary>
        /// <param name="features">The features to include in the configuration file if a default file has to be created.</param>
        /// <returns>Initialized node configuration.</returns>
        /// <exception cref="ConfigurationException">Thrown in case of any problems with the configuration file or command line arguments.</exception>
        public NodeSettings LoadConfiguration(List<IFeatureRegistration> features = null)
        {
            // Configuration already loaded?
            if (this.ConfigReader != null)
                return this;

            // Get the arguments set previously
            var args = this.LoadArgs;

            // If no configuration file path is passed in the args, load the default file.
            if (this.ConfigurationFile == null)
            {
                this.ConfigurationFile = this.CreateDefaultConfigurationFile(features);
            }

            // Add the file configuration to the command-line configuration.
            var fileConfig = new TextFileConfiguration(File.ReadAllText(this.ConfigurationFile));
            var config = new TextFileConfiguration(args);
            this.ConfigReader = config;
            fileConfig.MergeInto(config);

            // Set the configuration filter and file path.
            this.Log.Load(config);
            this.LoggerFactory.AddFilters(this.Log, this.DataFolder);
            this.LoggerFactory.ConfigureConsoleFilters(this.LoggerFactory.GetConsoleSettings(), this.Log);

            this.Logger.LogDebug("Data directory set to '{0}'.", this.DataDir);
            this.Logger.LogDebug("Configuration file set to '{0}'.", this.ConfigurationFile);

            this.RequireStandard = config.GetOrDefault("acceptnonstdtxn", !(this.Network.IsTest()));
            this.MaxTipAge = config.GetOrDefault("maxtipage", this.Network.MaxTipAge);
            this.Logger.LogDebug("Network: IsTest='{0}', IsBitcoin='{1}'.", this.Network.IsTest(), this.Network.IsBitcoin());
            this.MinTxFeeRate = new FeeRate(config.GetOrDefault("mintxfee", this.Network.MinTxFee));
            this.Logger.LogDebug("MinTxFeeRate set to {0}.", this.MinTxFeeRate);
            this.FallbackTxFeeRate = new FeeRate(config.GetOrDefault("fallbackfee", this.Network.FallbackFee));
            this.Logger.LogDebug("FallbackTxFeeRate set to {0}.", this.FallbackTxFeeRate);
            this.MinRelayTxFeeRate = new FeeRate(config.GetOrDefault("minrelaytxfee", this.Network.MinRelayTxFee));
            this.Logger.LogDebug("MinRelayTxFeeRate set to {0}.", this.MinRelayTxFeeRate);
            this.SyncTimeEnabled = config.GetOrDefault<bool>("synctime", true);
            this.Logger.LogDebug("Time synchronization with peers is {0}.", this.SyncTimeEnabled ? "enabled" : "disabled");

            // Add a prefix set by the user to the agent. This will allow people running nodes to
            // identify themselves if they wish. The prefix is limited to 10 characters.
            string agentPrefix = config.GetOrDefault("agentprefix", string.Empty);
            agentPrefix = agentPrefix.Substring(0, Math.Min(10, agentPrefix.Length));
            this.Agent = string.IsNullOrEmpty(agentPrefix) ? this.Agent : $"{agentPrefix}-{this.Agent}"; 

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
        /// <param name="features">The features to include in the configuration file if a default file has to be created.</param>
        /// <returns>Path to the configuration file.</returns>
        private string CreateDefaultConfigurationFile(List<IFeatureRegistration> features = null)
        {
            string configFilePath = Path.Combine(this.DataDir, this.Network.DefaultConfigFilename);
            this.Logger.LogDebug("Configuration file set to '{0}'.", configFilePath);

            // Create a config file if none exist.
            if (!File.Exists(configFilePath))
            {
                this.Logger.LogDebug("Creating configuration file...");

                StringBuilder builder = new StringBuilder();

                if (features != null)
                {
                    foreach (var featureRegistration in features)
                    {
                        MethodInfo getDefaultConfiguration = featureRegistration.FeatureType.GetMethod("BuildDefaultConfigurationFile", BindingFlags.Public | BindingFlags.Static);
                        if (getDefaultConfiguration != null)
                        {
                            getDefaultConfiguration.Invoke(null, new object[] { builder, this.Network });
                            builder.AppendLine();
                        }
                    }
                }

                File.WriteAllText(configFilePath, builder.ToString());
            }
            return configFilePath;
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
            directoryPath = Path.Combine(directoryPath, network.Name);
            Directory.CreateDirectory(directoryPath);

            this.Logger.LogDebug("Data directory initialized with path {0}.", directoryPath);
            return directoryPath;
        }

        /// <summary>
        /// Displays command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            Guard.NotNull(network, nameof(network));

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
            builder.AppendLine($"-maxtipage=<number>       Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"-connect=<ip:port>        Specified node to connect to. Can be specified multiple times.");
            builder.AppendLine($"-addnode=<ip:port>        Add a node to connect to and attempt to keep the connection open. Can be specified multiple times.");
            builder.AppendLine($"-whitebind=<ip:port>      Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6. Can be specified multiple times.");
            builder.AppendLine($"-externalip=<ip>          Specify your own public address.");
            builder.AppendLine($"-synctime=<0 or 1>        Sync with peers. Default 1.");
            builder.AppendLine($"-mintxfee=<number>        Minimum fee rate. Defaults to network specific value.");
            builder.AppendLine($"-fallbackfee=<number>     Fallback fee rate. Defaults to network specific value.");
            builder.AppendLine($"-minrelaytxfee=<number>   Minimum relay fee rate. Defaults to network specific value.");
            builder.AppendLine($"-bantime=<number>         Number of seconds to keep misbehaving peers from reconnecting (Default 24-hour ban).");

            defaults.Logger.LogInformation(builder.ToString());
        }
        
        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            var defaults = Default();

            builder.AppendLine("####Node Settings####");
            builder.AppendLine($"#Accept non-standard transactions. Default {(defaults.RequireStandard?1:0)}.");
            builder.AppendLine($"#acceptnonstdtxn={(defaults.RequireStandard?1:0)}");
            builder.AppendLine($"#Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"#maxtipage={network.MaxTipAge}");
            builder.AppendLine($"#Specified node to connect to. Can be specified multiple times.");
            builder.AppendLine($"#connect=<ip:port>");
            builder.AppendLine($"#Add a node to connect to and attempt to keep the connection open. Can be specified multiple times.");
            builder.AppendLine($"#addnode=<ip:port>");
            builder.AppendLine($"#Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6. Can be specified multiple times.");
            builder.AppendLine($"#whitebind=<ip:port>");
            builder.AppendLine($"#Specify your own public address.");
            builder.AppendLine($"#externalip=<ip>");
            builder.AppendLine($"#Sync with peers. Default 1.");
            builder.AppendLine($"#synctime=1");
            builder.AppendLine($"#Minimum fee rate. Defaults to {network.MinTxFee}.");
            builder.AppendLine($"#mintxfee={network.MinTxFee}");
            builder.AppendLine($"#Fallback fee rate. Defaults to {network.FallbackFee}.");
            builder.AppendLine($"#fallbackfee={network.FallbackFee}");
            builder.AppendLine($"#Minimum relay fee rate. Defaults to {network.MinRelayTxFee}.");
            builder.AppendLine($"#minrelaytxfee={network.MinRelayTxFee}");
            builder.AppendLine($"#Number of seconds to keep misbehaving peers from reconnecting (Default 24-hour ban).");
            builder.AppendLine($"#bantime=<number>");
        }
    }
}
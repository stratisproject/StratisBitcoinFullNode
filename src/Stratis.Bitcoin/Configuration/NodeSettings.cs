using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Networks;
using NBitcoin.Protocol;
using NLog.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;

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
    public class NodeSettings : IDisposable
    {
        /// <summary>Version of the protocol the current implementation supports.</summary>
        public const ProtocolVersion SupportedProtocolVersion = ProtocolVersion.SENDHEADERS_VERSION;

        /// <summary>Factory to create instance logger.</summary>
        public ILoggerFactory LoggerFactory { get; private set; }

        /// <summary>Instance logger.</summary>
        public ILogger Logger { get; private set; }

        /// <summary>Configuration related to logging.</summary>
        public LogSettings Log { get; private set; }

        /// <summary>List of paths to important files and folders.</summary>
        public DataFolder DataFolder { get; private set; }

        /// <summary>Path to the data directory. This value is read-only and is set in the constructor's args.</summary>
        public string DataDir { get; private set; }

        /// <summary>Path to the configuration file. This value is read-only and is set in the constructor's args.</summary>
        public string ConfigurationFile { get; private set; }

        /// <summary>Combined command line arguments and configuration file settings.</summary>
        public TextFileConfiguration ConfigReader { get; private set; }

        /// <summary>Supported protocol version.</summary>
        public ProtocolVersion ProtocolVersion { get; private set; }

        /// <summary>Lowest supported protocol version.</summary>
        public ProtocolVersion? MinProtocolVersion { get; set; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        /// <summary>The node's user agent.</summary>
        public string Agent { get; private set; }

        /// <summary>Minimum transaction fee for network.</summary>
        public FeeRate MinTxFeeRate { get; private set; }

        /// <summary>Fall back transaction fee for network.</summary>
        public FeeRate FallbackTxFeeRate { get; private set; }

        /// <summary>Minimum relay transaction fee for network.</summary>
        public FeeRate MinRelayTxFeeRate { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">The network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="protocolVersion">Supported protocol version for which to create the configuration.</param>
        /// <param name="agent">The nodes user agent that will be shared with peers.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="networksSelector">A selector class that delayed load a network for either - regtest/testnet/mainnet.</param>
        /// <exception cref="ConfigurationException">Thrown in case of any problems with the configuration file or command line arguments.</exception>
        /// <remarks>
        /// Processing depends on whether a configuration file is passed via the command line.
        /// There are two main scenarios here:
        /// - The configuration file is passed via the command line. In this case we need
        ///   to read it earlier so that it can provide defaults for "testnet" and "regtest".
        /// - Alternatively, if the file name is not supplied then a network-specific file
        ///   name would be determined. In this case we first need to determine the network.
        /// </remarks>
        public NodeSettings(Network network = null, ProtocolVersion protocolVersion = SupportedProtocolVersion,
            string agent = "StratisNode", string[] args = null, NetworksSelector networksSelector = null)
        {
            // Create the default logger factory and logger.
            var loggerFactory = new ExtendedLoggerFactory();
            this.LoggerFactory = loggerFactory;
            this.LoggerFactory.AddConsoleWithFilters();
            this.LoggerFactory.AddNLog();
            this.Logger = this.LoggerFactory.CreateLogger(typeof(NodeSettings).FullName);

            // Record arguments.
            this.Network = network;
            this.ProtocolVersion = protocolVersion;
            this.Agent = agent;
            this.ConfigReader = new TextFileConfiguration(args ?? new string[] { });

            // Log arguments.
            this.Logger.LogDebug("Arguments: network='{0}', protocolVersion='{1}', agent='{2}', args='{3}'.",
                this.Network == null ? "(None)" : this.Network.Name,
                this.ProtocolVersion,
                this.Agent,
                args == null ? "(None)" : string.Join(" ", args));

            // By default, we look for a file named '<network>.conf' in the network's data directory,
            // but both the data directory and the configuration file path may be changed using the -datadir and -conf command-line arguments.
            this.ConfigurationFile = this.ConfigReader.GetOrDefault<string>("conf", null, this.Logger)?.NormalizeDirectorySeparator();
            this.DataDir = this.ConfigReader.GetOrDefault<string>("datadir", null, this.Logger)?.NormalizeDirectorySeparator();

            // If the configuration file is relative then assume it is relative to the data folder and combine the paths.
            if (this.DataDir != null && this.ConfigurationFile != null)
            {
                bool isRelativePath = Path.GetFullPath(this.ConfigurationFile).Length > this.ConfigurationFile.Length;
                if (isRelativePath)
                    this.ConfigurationFile = Path.Combine(this.DataDir, this.ConfigurationFile);
            }

            // If the configuration file has been specified on the command line then read it now
            // so that it can provide the defaults for testnet and regtest.
            if (this.ConfigurationFile != null)
            {
                // If the configuration file was specified on the command line then it must exist.
                if (!File.Exists(this.ConfigurationFile))
                    throw new ConfigurationException($"Configuration file does not exist at {this.ConfigurationFile}.");

                // Sets the ConfigReader based on the arguments and the configuration file if it exists.
                this.ReadConfigurationFile();
            }

            // If the network is not known then derive it from the command line arguments.
            if (this.Network == null)
            {
                if (networksSelector == null)
                    throw new ConfigurationException("Network or NetworkSelector not provided.");

                // Find out if we need to run on testnet or regtest from the config file.
                bool testNet = this.ConfigReader.GetOrDefault<bool>("testnet", false, this.Logger);
                bool regTest = this.ConfigReader.GetOrDefault<bool>("regtest", false, this.Logger);

                if (testNet && regTest)
                    throw new ConfigurationException("Invalid combination of regtest and testnet.");

                this.Network = testNet ? networksSelector.Testnet() : regTest ? networksSelector.Regtest() : networksSelector.Mainnet();

                this.Logger.LogDebug("Network set to '{0}'.", this.Network.Name);
            }

            // Ensure the network being used is registered and we have the correct Network object reference.
            this.Network = NetworkRegistration.Register(this.Network);

            // Set the full data directory path.
            if (this.DataDir == null)
            {
                // Create the data directories if they don't exist.
                this.DataDir = this.CreateDefaultDataDirectories(Path.Combine("StratisNode", this.Network.RootFolderName), this.Network);
            }
            else
            {
                // Combine the data directory with the network's root folder and name.
                string directoryPath = Path.Combine(this.DataDir, this.Network.RootFolderName, this.Network.Name);
                this.DataDir = Directory.CreateDirectory(directoryPath).FullName;
                this.Logger.LogDebug("Data directory initialized with path {0}.", this.DataDir);
            }

            // Set the data folder.
            this.DataFolder = new DataFolder(this.DataDir);

            // Attempt to load NLog configuration from the DataFolder.
            loggerFactory.LoadNLogConfiguration(this.DataFolder);

            // Get the configuration file name for the network if it was not specified on the command line.
            if (this.ConfigurationFile == null)
            {
                this.ConfigurationFile = Path.Combine(this.DataDir, this.Network.DefaultConfigFilename);
                this.Logger.LogDebug("Configuration file set to '{0}'.", this.ConfigurationFile);

                if (File.Exists(this.ConfigurationFile))
                    this.ReadConfigurationFile();
            }

            // Create the custom logger factory.
            this.Log = new LogSettings();
            this.Log.Load(this.ConfigReader);
            this.LoggerFactory.AddFilters(this.Log, this.DataFolder);
            this.LoggerFactory.ConfigureConsoleFilters(this.LoggerFactory.GetConsoleSettings(), this.Log);

            // Load the configuration.
            this.LoadConfiguration();
        }

        /// <summary>Determines whether to print help and exit.</summary>
        public bool PrintHelpAndExit
        {
            get
            {
                return this.ConfigReader.GetOrDefault<bool>("help", false, this.Logger) ||
                    this.ConfigReader.GetOrDefault<bool>("-help", false, this.Logger);
            }
        }

        /// <summary>
        /// Initializes default configuration.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="protocolVersion">Supported protocol version for which to create the configuration.</param>
        /// <returns>Default node configuration.</returns>
        public static NodeSettings Default(Network network, ProtocolVersion protocolVersion = SupportedProtocolVersion)
        {
            return new NodeSettings(network, protocolVersion);
        }

        /// <summary>
        /// Creates the configuration file if it does not exist.
        /// </summary>
        /// <param name="features">The features for which to include settings in the configuration file.</param>
        public void CreateDefaultConfigurationFile(List<IFeatureRegistration> features)
        {
            // If the config file does not exist yet then create it now.
            if (!File.Exists(this.ConfigurationFile))
            {
                this.Logger.LogDebug("Creating configuration file '{0}'.", this.ConfigurationFile);

                var builder = new StringBuilder();

                foreach (IFeatureRegistration featureRegistration in features)
                {
                    MethodInfo getDefaultConfiguration = featureRegistration.FeatureType.GetMethod("BuildDefaultConfigurationFile", BindingFlags.Public | BindingFlags.Static);
                    if (getDefaultConfiguration != null)
                    {
                        getDefaultConfiguration.Invoke(null, new object[] { builder, this.Network });
                        builder.AppendLine();
                    }
                }

                File.WriteAllText(this.ConfigurationFile, builder.ToString());
                this.ReadConfigurationFile();
                this.LoadConfiguration();
            }
        }

        /// <summary>
        /// Reads the configuration file and merges it with the command line arguments.
        /// </summary>
        private void ReadConfigurationFile()
        {
            this.Logger.LogDebug("Reading configuration file '{0}'.", this.ConfigurationFile);

            // Add the file configuration to the command-line configuration.
            var fileConfig = new TextFileConfiguration(File.ReadAllText(this.ConfigurationFile));
            fileConfig.MergeInto(this.ConfigReader);
        }

        /// <summary>
        /// Loads the node settings from the application configuration.
        /// </summary>
        private void LoadConfiguration()
        {
            TextFileConfiguration config = this.ConfigReader;

            this.MinTxFeeRate = new FeeRate(config.GetOrDefault("mintxfee", this.Network.MinTxFee, this.Logger));
            this.FallbackTxFeeRate = new FeeRate(config.GetOrDefault("fallbackfee", this.Network.FallbackFee, this.Logger));
            this.MinRelayTxFeeRate = new FeeRate(config.GetOrDefault("minrelaytxfee", this.Network.MinRelayTxFee, this.Logger));
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
                string home = Environment.GetEnvironmentVariable("HOME");
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
                string localAppData = Environment.GetEnvironmentVariable("APPDATA");
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

            NodeSettings defaults = Default(network: network);
            string daemonName = Path.GetFileName(Assembly.GetEntryAssembly().Location);

            var builder = new StringBuilder();
            builder.AppendLine("Usage:");
            builder.AppendLine($" dotnet run {daemonName} [arguments]");
            builder.AppendLine();
            builder.AppendLine("Command line arguments:");
            builder.AppendLine();
            builder.AppendLine($"-help/--help              Show this help.");
            builder.AppendLine($"-conf=<Path>              Path to the configuration file. Defaults to {defaults.ConfigurationFile}.");
            builder.AppendLine($"-datadir=<Path>           Path to the data directory. Defaults to {defaults.DataDir}.");
            builder.AppendLine($"-debug[=<string>]         Set 'Debug' logging level. Specify what to log via e.g. '-debug=Stratis.Bitcoin.Miner,Stratis.Bitcoin.Wallet'.");
            builder.AppendLine($"-loglevel=<string>        Direct control over the logging level: '-loglevel=trace/debug/info/warn/error/fatal'.");

            // Can be overridden in configuration file.
            builder.AppendLine($"-testnet                  Use the testnet chain.");
            builder.AppendLine($"-regtest                  Use the regtestnet chain.");
            builder.AppendLine($"-mintxfee=<number>        Minimum fee rate. Defaults to {network.MinTxFee}.");
            builder.AppendLine($"-fallbackfee=<number>     Fallback fee rate. Defaults to {network.FallbackFee}.");
            builder.AppendLine($"-minrelaytxfee=<number>   Minimum relay fee rate. Defaults to {network.MinRelayTxFee}.");

            defaults.Logger.LogInformation(builder.ToString());

            ConnectionManagerSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Node Settings####");
            builder.AppendLine($"#Test network. Defaults to 0.");
            builder.AppendLine($"testnet={((network.IsTest() && !network.IsRegTest()) ? 1 : 0)}");
            builder.AppendLine($"#Regression test network. Defaults to 0.");
            builder.AppendLine($"regtest={(network.IsRegTest() ? 1 : 0)}");
            builder.AppendLine($"#Minimum fee rate. Defaults to {network.MinTxFee}.");
            builder.AppendLine($"#mintxfee={network.MinTxFee}");
            builder.AppendLine($"#Fallback fee rate. Defaults to {network.FallbackFee}.");
            builder.AppendLine($"#fallbackfee={network.FallbackFee}");
            builder.AppendLine($"#Minimum relay fee rate. Defaults to {network.MinRelayTxFee}.");
            builder.AppendLine($"#minrelaytxfee={network.MinRelayTxFee}");
            builder.AppendLine();

            ConnectionManagerSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.LoggerFactory.Dispose();
        }
    }
}
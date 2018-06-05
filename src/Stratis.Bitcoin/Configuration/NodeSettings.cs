using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; private set; }

        /// <summary>The node's user agent.</summary>
        public string Agent { get; set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">The network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="protocolVersion">Supported protocol version for which to create the configuration.</param>
        /// <param name="agent">The nodes user agent that will be shared with peers.</param>
        /// <param name="args">The command-line arguments.</param>
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
            string agent = "StratisBitcoin", string[] args = null)
        {
            // Create the default logger factory and logger.
            this.LoggerFactory = new ExtendedLoggerFactory();
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
                args == null?"(None)":string.Join(" ", args));

            // By default, we look for a file named '<network>.conf' in the network's data directory,
            // but both the data directory and the configuration file path may be changed using the -datadir and -conf command-line arguments.
            this.ConfigurationFile = this.ConfigReader.GetOrDefault<string>("conf", null)?.NormalizeDirectorySeparator();
            this.DataDir = this.ConfigReader.GetOrDefault<string>("datadir",  null)?.NormalizeDirectorySeparator();        

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
                // Find out if we need to run on testnet or regtest from the config file.
                var testNet = this.ConfigReader.GetOrDefault<bool>("testnet", false);
                var regTest = this.ConfigReader.GetOrDefault<bool>("regtest", false);

                this.Logger.LogDebug("Network type: testnet='{0}', regtest='{1}'.", testNet, regTest);

                if (testNet && regTest)
                    throw new ConfigurationException("Invalid combination of regtest and testnet.");

                if (protocolVersion == ProtocolVersion.ALT_PROTOCOL_VERSION)
                    this.Network = testNet ? Network.StratisTest : regTest ? Network.StratisRegTest : Network.StratisMain;
                else
                    this.Network = testNet ? Network.TestNet : regTest ? Network.RegTest : Network.Main;

                this.Logger.LogDebug("Network set to '{0}'.", this.Network.Name);
            }

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
        }

        /// <summary>Determines whether to print help and exit.</summary>
        public bool PrintHelpAndExit
        {
            get
            {
                return this.ConfigReader.GetOrDefault<bool>("help", false) ||
                    this.ConfigReader.GetOrDefault<bool>("-help", false);
            }
        }

        /// <summary>
        /// Initializes default configuration.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="protocolVersion">Supported protocol version for which to create the configuration.</param>
        /// <returns>Default node configuration.</returns>
        public static NodeSettings Default(Network network = null, ProtocolVersion protocolVersion = SupportedProtocolVersion)
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

                StringBuilder builder = new StringBuilder();

                foreach (var featureRegistration in features)
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
    }
}
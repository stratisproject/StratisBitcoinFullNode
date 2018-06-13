using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configuration related to incoming and outgoing connections.
    /// </summary>
    public sealed class ConnectionManagerSettings
    {
        /// <summary>Number of seconds to keep misbehaving peers from reconnecting (Default 24-hour ban).</summary>
        public const int DefaultMisbehavingBantimeSeconds = 24 * 60 * 60;
        public const int DefaultMaxOutboundConnections = 8;

        /// <summary>Maximum number of AgentPrefix characters to use in the Agent value.</summary>
        private const int MaximumAgentPrefixLength = 10;

        /// <summary>Default value for "blocksonly" option.</summary>
        /// <seealso cref="RelayTxes"/>
        private const bool DefaultBlocksOnly = false;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes an instance of the object from the default configuration.
        /// </summary>
        public ConnectionManagerSettings() : this(NodeSettings.Default())
        {
        }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public ConnectionManagerSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ConnectionManagerSettings).FullName);
            this.logger.LogTrace("({0}:'{1}')", nameof(nodeSettings), nodeSettings.Network.Name);

            this.Connect = new List<IPEndPoint>();
            this.AddNode = new List<IPEndPoint>();
            this.Listen = new List<NodeServerEndpoint>();

            TextFileConfiguration config = nodeSettings.ConfigReader;

            try
            {
                this.Connect.AddRange(config.GetAll("connect", this.logger)
                    .Select(c => c.ToIPEndPoint(nodeSettings.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'connect' parameter.");
            }

            try
            {
                this.AddNode.AddRange(config.GetAll("addnode", this.logger)
                        .Select(c => c.ToIPEndPoint(nodeSettings.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'addnode' parameter.");
            }

            int port = config.GetOrDefault<int>("port", nodeSettings.Network.DefaultPort, this.logger);
            try
            {
                this.Listen.AddRange(config.GetAll("bind")
                        .Select(c => new NodeServerEndpoint(c.ToIPEndPoint(port), false)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'bind' parameter");
            }

            try
            {
                this.Listen.AddRange(config.GetAll("whitebind", this.logger)
                        .Select(c => new NodeServerEndpoint(c.ToIPEndPoint(port), true)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'listen' parameter");
            }

            if (this.Listen.Count == 0)
            {
                this.Listen.Add(new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port), false));
            }

            string externalIp = config.GetOrDefault<string>("externalip", null, this.logger);
            if (externalIp != null)
            {
                try
                {
                    this.ExternalEndpoint = externalIp.ToIPEndPoint(port);
                }
                catch (FormatException)
                {
                    throw new ConfigurationException("Invalid 'externalip' parameter");
                }
            }

            if (this.ExternalEndpoint == null)
            {
                this.ExternalEndpoint = new IPEndPoint(IPAddress.Loopback, nodeSettings.Network.DefaultPort);
            }

            this.BanTimeSeconds = config.GetOrDefault<int>("bantime", ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds, this.logger);
            this.MaxOutboundConnections = config.GetOrDefault<int>("maxoutboundconnections", ConnectionManagerSettings.DefaultMaxOutboundConnections, this.logger);
            this.BurstModeTargetConnections = config.GetOrDefault("burstModeTargetConnections", 1, this.logger);
            this.SyncTimeEnabled = config.GetOrDefault<bool>("synctime", true, this.logger);
            this.RelayTxes = !config.GetOrDefault("blocksonly", DefaultBlocksOnly, this.logger);

            var agentPrefix = config.GetOrDefault("agentprefix", string.Empty, this.logger).Replace("-", "");
            if (agentPrefix.Length > MaximumAgentPrefixLength)
                agentPrefix = agentPrefix.Substring(0, MaximumAgentPrefixLength);

            this.Agent = string.IsNullOrEmpty(agentPrefix) ? nodeSettings.Agent : $"{agentPrefix}-{nodeSettings.Agent}";
            this.logger.LogDebug("Agent set to '{0}'.", this.Agent);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            var defaults = NodeSettings.Default(network: network);
            builder.AppendLine("####ConnectionManager Settings####");
            builder.AppendLine($"#The default network port to connect to. Default { network.DefaultPort }.");
            builder.AppendLine($"#port={network.DefaultPort}");
            builder.AppendLine($"#Specified node to connect to. Can be specified multiple times.");
            builder.AppendLine($"#connect=<ip:port>");
            builder.AppendLine($"#Add a node to connect to and attempt to keep the connection open. Can be specified multiple times.");
            builder.AppendLine($"#addnode=<ip:port>");
            builder.AppendLine($"#Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6. Can be specified multiple times.");
            builder.AppendLine($"#whitebind=<ip:port>");
            builder.AppendLine($"#Specify your own public address.");
            builder.AppendLine($"#externalip=<ip>");
            builder.AppendLine($"#Number of seconds to keep misbehaving peers from reconnecting. Default {ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds}.");
            builder.AppendLine($"#bantime=<number>");
            builder.AppendLine($"#The maximum number of outbound connections. Default {ConnectionManagerSettings.DefaultMaxOutboundConnections}.");
            builder.AppendLine($"#maxoutboundconnections=<number>");
            builder.AppendLine($"#Sync with peers. Default 1.");
            builder.AppendLine($"#synctime=1");
            builder.AppendLine($"#An optional prefix for the node's user agent shared with peers. Truncated if over { MaximumAgentPrefixLength } characters.");
            builder.AppendLine($"#agentprefix=<string>");
            builder.AppendLine($"#Enable bandwidth saving setting to send and received confirmed blocks only. Defaults to { (DefaultBlocksOnly ? 1 : 0) }.");
            builder.AppendLine($"#blocksonly={ (DefaultBlocksOnly ? 1 : 0) }");
        }

        /// <summary>
        /// Displays command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            Guard.NotNull(network, nameof(network));

            var defaults = NodeSettings.Default(network: network);

            var builder = new StringBuilder();
            builder.AppendLine($"-port=<port>              The default network port to connect to. Default { network.DefaultPort }.");
            builder.AppendLine($"-connect=<ip:port>        Specified node to connect to. Can be specified multiple times.");
            builder.AppendLine($"-addnode=<ip:port>        Add a node to connect to and attempt to keep the connection open. Can be specified multiple times.");
            builder.AppendLine($"-whitebind=<ip:port>      Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6. Can be specified multiple times.");
            builder.AppendLine($"-externalip=<ip>          Specify your own public address.");
            builder.AppendLine($"-bantime=<number>         Number of seconds to keep misbehaving peers from reconnecting. Default {ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds}.");
            builder.AppendLine($"-maxoutboundconnections=<number> The maximum number of outbound connections. Default {ConnectionManagerSettings.DefaultMaxOutboundConnections}.");
            builder.AppendLine($"-synctime=<0 or 1>        Sync with peers. Default 1.");
            builder.AppendLine($"-agentprefix=<string>     An optional prefix for the node's user agent that will be shared with peers in the version handshake.");
            builder.AppendLine($"-blocksonly=<0 or 1>      Enable bandwidth saving setting to send and received confirmed blocks only. Defaults to { DefaultBlocksOnly }.");

            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>List of exclusive end points that the node should be connected to.</summary>
        public List<IPEndPoint> Connect { get; set; }

        /// <summary>List of end points that the node should try to connect to.</summary>
        public List<IPEndPoint> AddNode { get; set; }

        /// <summary>List of network interfaces on which the node should listen on.</summary>
        public List<NodeServerEndpoint> Listen { get; set; }

        /// <summary>External (or public) IP address of the node.</summary>
        public IPEndPoint ExternalEndpoint { get; internal set; }

        /// <summary>Number of seconds to keep misbehaving peers from reconnecting.</summary>
        public int BanTimeSeconds { get; internal set; }

        /// <summary>Maximum number of outbound connections.</summary>
        public int MaxOutboundConnections { get; internal set; }

        /// <summary>Connections number after which burst connectivity mode (connection attempts with no delay in between) will be disabled.</summary>
        public int BurstModeTargetConnections { get; internal set; }

        /// <summary><c>true</c> to sync time with other peers and calculate adjusted time, <c>false</c> to use our system clock only.</summary>
        public bool SyncTimeEnabled { get; private set; }

        /// <summary>The node's user agent.</summary>
        public string Agent { get; private set; }

        /// <summary><c>true</c> to enable bandwidth saving setting to send and received confirmed blocks only.</summary>
        public bool RelayTxes { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
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
        public const int MaximumAgentPrefixLength = 10;

        /// <summary>The node's user agent. Includes the prefix if "agentprefix" is specified.</summary>
        public string Agent { get; set; }

        /// <summary><c>true</c> to sync time with other peers and calculate adjusted time, <c>false</c> to use our system clock only.</summary>
        public bool SyncTimeEnabled { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConnectionManagerSettings()
        {
            this.Connect = new List<IPEndPoint>();
            this.AddNode = new List<IPEndPoint>();
            this.Listen = new List<NodeServerEndpoint>();
        }

        /// <summary>
        /// Loads the ConnectionManager related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public ConnectionManagerSettings Load(NodeSettings nodeSettings)
        {
            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ConnectionManagerSettings).FullName);

            logger.LogTrace("()");

            var config = nodeSettings.ConfigReader;

            try
            {
                this.Connect.AddRange(config.GetAll("connect")
                    .Select(c => c.ToIPEndPoint(nodeSettings.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'connect' parameter.");
            }

            try
            {
                this.AddNode.AddRange(config.GetAll("addnode")
                        .Select(c => c.ToIPEndPoint(nodeSettings.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'addnode' parameter.");
            }

            var port = config.GetOrDefault<int>("port", nodeSettings.Network.DefaultPort);
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
                this.Listen.AddRange(config.GetAll("whitebind")
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

            var externalIp = config.GetOrDefault<string>("externalip", null);
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

            this.BanTimeSeconds = config.GetOrDefault<int>("bantime", ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds);
            logger.LogDebug("BanTimeSeconds set to {0}.", this.BanTimeSeconds);

            this.MaxOutboundConnections = config.GetOrDefault<int>("maxoutboundconnections", ConnectionManagerSettings.DefaultMaxOutboundConnections);
            logger.LogDebug("MaxOutboundConnections set to {0}.", this.MaxOutboundConnections);

            this.BurstModeTargetConnections = config.GetOrDefault("burstModeTargetConnections", 1);
            logger.LogDebug("BurstModeTargetConnections set to {0}.", this.BurstModeTargetConnections);

            this.SyncTimeEnabled = config.GetOrDefault<bool>("synctime", true);
            logger.LogDebug("Time synchronization with peers is {0}.", this.SyncTimeEnabled ? "enabled" : "disabled");

            var agentPrefix = config.GetOrDefault("agentprefix", string.Empty).Replace("-", "");

            if (agentPrefix.Length > MaximumAgentPrefixLength)
                agentPrefix = agentPrefix.Substring(0, MaximumAgentPrefixLength);
            logger.LogDebug("AgentPrefix set to {0}.", agentPrefix);

            this.Agent = string.IsNullOrEmpty(agentPrefix) ? nodeSettings.Agent : $"{agentPrefix}-{nodeSettings.Agent}";
            logger.LogDebug("Agent set to {0}.", this.Agent);

            logger.LogTrace("(-)");

            return this;
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
            builder.AppendLine($"-agentprefix=<string>     An optional prefix for the node's user agent that will be shared with peers in the version handshake.");
            builder.AppendLine($"-synctime=<0 or 1>        Sync with peers. Default 1.");

            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
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
        }
    }
}
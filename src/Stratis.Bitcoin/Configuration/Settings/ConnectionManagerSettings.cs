using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

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
        public void Load(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;

            try
            {
                this.Connect.AddRange(config.GetAll("connect")
                    .Select(c => NodeSettings.ConvertIpAddressToEndpoint(c, nodeSettings.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'connect' parameter.");
            }

            try
            {
                this.AddNode.AddRange(config.GetAll("addnode")
                        .Select(c => NodeSettings.ConvertIpAddressToEndpoint(c, nodeSettings.Network.DefaultPort)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'addnode' parameter.");
            }

            var port = config.GetOrDefault<int>("port", nodeSettings.Network.DefaultPort);
            try
            {
                this.Listen.AddRange(config.GetAll("bind")
                        .Select(c => new NodeServerEndpoint(NodeSettings.ConvertIpAddressToEndpoint(c, port), false)));
            }
            catch (FormatException)
            {
                throw new ConfigurationException("Invalid 'bind' parameter");
            }

            try
            {
                this.Listen.AddRange(config.GetAll("whitebind")
                        .Select(c => new NodeServerEndpoint(NodeSettings.ConvertIpAddressToEndpoint(c, port), true)));
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
                    this.ExternalEndpoint = NodeSettings.ConvertIpAddressToEndpoint(externalIp, port);
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
            this.MaxOutboundConnections = config.GetOrDefault<int>("maxoutboundconnections", ConnectionManagerSettings.DefaultMaxOutboundConnections);
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
    }
}
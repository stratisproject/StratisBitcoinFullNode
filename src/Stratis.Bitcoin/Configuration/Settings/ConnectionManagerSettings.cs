using System.Collections.Generic;
using System.Net;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configuration related to incoming and outgoing connections.
    /// </summary>
    public sealed class ConnectionManagerSettings
    {
        /// <summary>Number of seconds to keep misbehaving peers from reconnecting (Default 24-hour ban).</summary>
        public const int DefaultMisbehavingBantimeSeconds = 24 * 60 * 60;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConnectionManagerSettings()
        {
            this.Connect = new IPEndPointSet();
            this.AddNode = new IPEndPointSet();
            this.Listen = new List<NodeServerEndpoint>();
        }

        /// <summary>List of exclusive end points that the node should be connected to.</summary>
        public IPEndPointSet Connect { get; set; }

        /// <summary>List of end points that the node should try to connect to.</summary>
        public IPEndPointSet AddNode { get; set; }

        /// <summary>List of network interfaces on which the node should listen on.</summary>
        public List<NodeServerEndpoint> Listen { get; set; }

        /// <summary>External (or public) IP address of the node.</summary>
        public IPEndPoint ExternalEndpoint { get; internal set; }

        /// <summary>Number of seconds to keep misbehaving peers from reconnecting.</summary>
        public int BanTimeSeconds { get; internal set; }
    }
}
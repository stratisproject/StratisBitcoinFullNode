using System.Collections.Generic;
using System.Net;

namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configuration related to incoming and outgoing connections.
    /// </summary>
    public class ConnectionManagerSettings
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConnectionManagerSettings()
        {
            this.Connect = new List<IPEndPoint>();
            this.AddNode = new List<IPEndPoint>();
            this.Listen = new List<NodeServerEndpoint>();
        }

        /// <summary>List of exclusive end points that the node should be connected to.</summary>
        public List<IPEndPoint> Connect { get; set; } 

        /// <summary>List of end points that the node should try to connect to.</summary>
        public List<IPEndPoint> AddNode { get; set; }

        /// <summary>List of network interfaces on which the node should listen on.</summary>
        public List<NodeServerEndpoint> Listen { get; set; }

        /// <summary>External (or public) IP address of the node.</summary>
        public IPEndPoint ExternalEndpoint { get; internal set; }
    }
}
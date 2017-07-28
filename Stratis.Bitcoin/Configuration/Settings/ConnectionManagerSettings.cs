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
        }

        /// <summary>List of exclusive end points that the node should be connected to.</summary>
        public List<IPEndPoint> Connect
        {
            get; set;
        } = new List<IPEndPoint>();

        /// <summary>List of end points that the node should try to connect to.</summary>
        public List<IPEndPoint> AddNode
        {
            get; set;
        } = new List<IPEndPoint>();

        /// <summary>List of network interfaces on which the node should listen on.</summary>
        public List<NodeServerEndpoint> Listen
        {
            get; set;
        } = new List<NodeServerEndpoint>();

        /// <summary>External (or public) IP address of the node.</summary>
        public IPEndPoint ExternalEndpoint
        {
            get;
            internal set;
        }
    }
}
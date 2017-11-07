using System.Net;

namespace Stratis.Bitcoin.Configuration
{
    /// <summary>
    /// Description of network interface on which the node listens.
    /// </summary>
    public class NodeServerEndpoint
    {
        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="endpoint">IP address and port number on which the node server listens.</param>
        /// <param name="whitelisted">If <c>true</c>, peers that connect to this interface are whitelisted.</param>
        public NodeServerEndpoint(IPEndPoint endpoint, bool whitelisted)
        {
            this.Endpoint = endpoint;
            this.Whitelisted = whitelisted;
        }

        /// <summary>IP address and port number on which the node server listens.</summary>
        public IPEndPoint Endpoint { get; set; }

        /// <summary>If <c>true</c>, peers that connect to this interface are whitelisted.</summary>
        public bool Whitelisted { get; set; }
    }
}

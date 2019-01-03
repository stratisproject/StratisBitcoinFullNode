using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin
{
    /// <summary>
    /// Holds default settings for nodes on this network.
    /// </summary>
    /// <remarks>
    /// TODO: Add AncestorSizeLimit variations, and anything else used as a default in a Settings object.
    /// </remarks>
    public class NodeDefaults
    {
        /// <summary>
        /// The default maximum number of outbound connections a node on this network will form.
        /// </summary>
        public int MaxOutboundConnections { get; }

        /// <summary>
        /// The default maximum number of inbound connections a node on this network will accept.
        /// </summary>
        public int MaxInboundConnections { get; }

        /// <summary>
        /// The default setting for whether nodes on this network accept transactions that are not of a 'standard' type.
        /// </summary>
        public bool AcceptNonStandardTransactions { get; }

        public NodeDefaults(
            int maxOutboundConnections,
            int maxInboundConnections,
            bool acceptNonStandardTransactions)
        {
            this.MaxOutboundConnections = maxOutboundConnections;
            this.MaxInboundConnections = maxInboundConnections;
            this.AcceptNonStandardTransactions = acceptNonStandardTransactions;
        }
    }
}

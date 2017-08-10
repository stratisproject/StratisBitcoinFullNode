using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Puller that download blocks from peers.
    /// </summary>
    public class IndexBlockPuller : StoreBlockPuller
    {
        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a list of available nodes. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="nodes">Network peers of the node.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public IndexBlockPuller(ConcurrentChain chain, Connection.IConnectionManager nodes, ILoggerFactory loggerFactory)
            : base(chain, nodes, loggerFactory)
        {
        }
    }
}

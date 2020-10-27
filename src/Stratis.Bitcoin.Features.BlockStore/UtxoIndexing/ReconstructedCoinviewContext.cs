using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// This is so named to indicate that it is not really intended for use outside of the block store's controller, i.e. it is not part of consensus.
    /// </summary>
    public class ReconstructedCoinviewContext
    {
        /// <summary>
        /// All of the outputs that haven't been spent at this point in time.
        /// </summary>
        public HashSet<OutPoint> UnspentOutputs { get; }

        /// <summary>
        /// Easy access to all of the loaded transactions.
        /// </summary>
        public Dictionary<uint256, Transaction> Transactions { get; }

        public ReconstructedCoinviewContext()
        {
            this.UnspentOutputs = new HashSet<OutPoint>();
            this.Transactions = new Dictionary<uint256, Transaction>();
        }
    }
}

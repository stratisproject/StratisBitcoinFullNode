using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Return value of <see cref="CoinView.FetchCoinsAsync(uint256[])"/>,
    /// contains the coinview tip's hash and information about unspent coins in the requested transactions.
    /// </summary>
    public class FetchCoinsResponse
    {
        /// <summary>Hash of the block header for which <see cref="UnspentOutputs"/> is related.</summary>
        public uint256 BlockHash { get; private set; }

        /// <summary>Unspent outputs of the requested transactions.</summary>
        public UnspentOutputs[] UnspentOutputs { get; private set; }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="unspent">Unspent outputs of the requested transactions.</param>
        /// <param name="blockHash">Block hash of the coinview's current tip.</param>
        public FetchCoinsResponse(UnspentOutputs[] unspent, uint256 blockHash)
        {
            Guard.NotNull(unspent, nameof(unspent));
            Guard.NotNull(blockHash, nameof(blockHash));

            this.BlockHash = blockHash;
            this.UnspentOutputs = unspent;
        }
    }
}

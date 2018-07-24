using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    // The default setting of values on the consensus options
    // should be removed in to the initialization of each
    // network this are network specific values
    public class PosConsensusOptions : ConsensusOptions
    {
        /// <summary>Coinstake minimal confirmations softfork activation height for the mainnet.</summary>
        internal const int CoinstakeMinConfirmationActivationHeightMainnet = 1005000;

        /// <summary>Coinstake minimal confirmations softfork activation height for the testnet.</summary>
        internal const int CoinstakeMinConfirmationActivationHeightTestnet = 436000;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public PosConsensusOptions()
        {
        }

        /// <summary>
        /// Gets the minimum confirmations amount required for a coin to be good enough to participate in staking.
        /// </summary>
        /// <param name="height">Block height.</param>
        /// <param name="network">The network.</param>
        public virtual int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.IsTest())
                return height < CoinstakeMinConfirmationActivationHeightTestnet ? 10 : 20;

            return height < CoinstakeMinConfirmationActivationHeightMainnet ? 50 : 500;
        }
    }
}
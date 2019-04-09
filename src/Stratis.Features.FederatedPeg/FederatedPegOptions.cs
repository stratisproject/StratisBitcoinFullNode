using NBitcoin;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>
    /// Holds information about the peg, including the details of the network on the other side of the peg.
    /// </summary>
    public class FederatedPegOptions
    {
        /// <summary>
        /// The chain that we are connecting to from this node.
        /// E.g. if this is a Cirrus sidechain gateway node, the counter-chain would be Stratis and vice versa.
        /// </summary>
        public Network CounterChainNetwork { get; }

        /// <summary>
        /// The height to start syncing the wallet from.
        /// </summary>
        public int WalletSyncFromHeight { get; }

        public FederatedPegOptions(Network counterChainNetwork, int walletSyncFromHeight = 1)
        {
            this.CounterChainNetwork = counterChainNetwork;
            this.WalletSyncFromHeight = walletSyncFromHeight;
        }
    }
}

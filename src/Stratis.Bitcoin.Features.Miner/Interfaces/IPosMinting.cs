using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner.Interfaces
{
    /// <summary>
    /// <see cref="PosMinting"/> is used in order to generate new blocks. It involves a sort of lottery, similar to proof-of-work,
    /// but the chances of winning this lottery is proportional to how many coins you are staking, not on hashing power.
    /// </summary>
    public interface IPosMinting
    {
        /// <summary>
        /// Creates a coinstake transaction with kernel that satisfies POS staking target.
        /// </summary>
        /// <param name="utxoStakeDescriptions">List of UTXOs that are available in the wallet for staking.</param>
        /// <param name="block">Template of the block that we are trying to mine.</param>
        /// <param name="chainTip">Tip of the best chain.</param>
        /// <param name="searchInterval">Length of an unexplored block time space in seconds. It only makes sense to look for a solution within this interval.</param>
        /// <param name="fees">Transaction fees from the transactions included in the block if we mine it.</param>
        /// <param name="coinstakeContext">Information about coinstake transaction and its private key that is to be filled when the kernel is found.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> otherwise.</returns>
        Task<bool> CreateCoinstakeAsync(List<PosMinting.UtxoStakeDescription> utxoStakeDescriptions, Block block, ChainedBlock chainTip, long searchInterval, long fees, PosMinting.CoinstakeContext coinstakeContext);

        /// <summary>
        /// Attempts to stake new blocks in a loop.
        /// <para>
        /// Staking is attempted only if the node is fully synchronized and connected to the network.
        /// </para>
        /// </summary>
        /// <param name="walletSecret">Credentials to the wallet with which will be used for staking.</param>
        Task GenerateBlocksAsync(PosMinting.WalletSecret walletSecret);

        /// <summary>
        /// Calculates staking difficulty for a specific block.
        /// </summary>
        /// <param name="block">Block at which to calculate the difficulty.</param>
        /// <returns>The staking difficulty. If there is no block to determine the staking difficulty the value 1.0 is returned.</returns>
        /// <remarks>
        /// The algorithm combines multiple exponential and simple moving averages to smooth difficulty readjustments.
        /// It has a fixed 24-block difficulty adjustment window for which it determines the average difficulty during the block window 
        /// using a linear adjustment (directly scaling by the ratio of actual and target time).
        /// The algorithm then uses the block header bits to calculate a maximum shift that allows the difficulty to shift either up or down. 
        /// It can shift in either direction to the same fixed factor.
        /// The algorithm was ported from <see cref="https://github.com/stratisproject/stratisX/blob/47851b7337f528f52ec20e86dca7dcead8191cf5/src/rpcblockchain.cpp#L16"/>
        /// which in turn was ported from darkcoin and/or bitcoin <see cref="https://github.com/bitcoin/bitcoin/blob/0.15/src/rpc/blockchain.cpp#L49"/>.
        /// Additional information on general difficulty calculations: <see cref="https://en.bitcoin.it/wiki/Difficulty"/> and 
        /// for some description regarding possible adjustments to make <see cref="http://web.archive.org/web/20180209161330/https://github.com/zcash/zcash/issues/147#issuecomment-202829008"/>.
        double GetDifficulty(ChainedBlock block);

        /// <summary>
        /// Constructs model for RPC "getstakinginfo" call.
        /// </summary>
        /// <returns>Staking information RPC response.</returns>
        Models.GetStakingInfoModel GetStakingInfoModel();

        /// <summary>
        /// Estimates the total staking weight of the network.
        /// </summary>
        /// <returns>Estimated amount of money that is used by all stakers on the network.</returns>
        /// <remarks>
        /// The idea behind estimating the network staking weight is very similar to estimating
        /// the total hash power of PoW network. The difficulty retarget algorithm tries to make
        /// sure of certain distribution of the blocks over a period of time. Base on real distribution
        /// and using the actual difficulty targets, one is able to compute how much stake was
        /// presented on the network to generate each block.
        /// <para>
        /// The method was ported from
        /// <see cref="https://github.com/stratisproject/stratisX/blob/47851b7337f528f52ec20e86dca7dcead8191cf5/src/rpcblockchain.cpp#L74"/>.
        /// </para>
        /// </remarks>
        double GetNetworkWeight();

        /// <summary>
        /// Starts the main POS staking loop.
        /// </summary>
        /// <param name="walletSecret">Credentials to the wallet with which will be used for staking.</param>
        void Stake(PosMinting.WalletSecret walletSecret);

        /// <summary>
        /// Stop the main POS staking loop.
        /// </summary>
        void StopStake();
    }
}

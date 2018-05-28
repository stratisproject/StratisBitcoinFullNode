using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Miner.Interfaces
{
    public interface IPowMining
    {
        /// <summary>
        /// Generates up to a specified number of blocks with a limited number of attempts.
        /// </summary>
        /// <param name="reserveScript">The reserve script.</param>
        /// <param name="generate">Number of blocks to generate. It is possible that less than the required number of blocks will be mined.</param>
        /// <param name="maxTries">Maximum number of attempts the miner will calculate PoW hash in order to find suitable ones to generate specified amount of blocks.</param>
        /// <returns>List with generated block's hashes</returns>
        List<uint256> GenerateBlocks(ReserveScript reserveScript, ulong generate, ulong maxTries);

        /// <summary>
        /// Increments or resets the extra nonce based on the previous hash block value on on the pow miner and the passed nExtraNonce.       
        /// </summary>
        /// <param name="pblock">The template block.</param>
        /// <param name="pindexPrev">The previous chained block.</param>
        /// <param name="nExtraNonce">The extra nonce counter.</param>
        /// <returns>The new extra nonce after incrementing.</returns>
        int IncrementExtraNonce(Block pblock, ChainedHeader pindexPrev, int nExtraNonce);

        /// <summary>
        /// Starts a new async mining loop or returns the existing running mining loop.
        /// </summary>
        /// <param name="reserveScript">The reserve script to use in the mining loop.</param>
        /// <returns>The running async loop.</returns>
        void Mine(Script reserveScript);

        /// <summary>
        /// Stops the async mining loop.
        /// </summary>
        void StopMining();
    }
}
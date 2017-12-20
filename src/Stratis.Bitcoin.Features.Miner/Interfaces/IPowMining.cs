using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner.Interfaces
{
    public interface IPowMining
    {
        /// <summary>
        /// Generates up to a specified number of blocks with a limited number of attempts.
        /// </summary>
        /// <param name="reserveScript"></param>
        /// <param name="generate">Number of blocks to generate. It is possible that less than the required number of blocks will be mined.</param>
        /// <param name="maxTries">Maximum number of attempts the miner will calculate PoW hash in order to find suitable ones to generate specified amount of blocks.</param>
        /// <returns>List with generated block's hashes</returns>
        List<uint256> GenerateBlocks(ReserveScript reserveScript, ulong generate, ulong maxTries);

        void IncrementExtraNonce(Block pblock, ChainedBlock pindexPrev, int nExtraNonce);

        IAsyncLoop Mine(Script reserveScript);
    }
}
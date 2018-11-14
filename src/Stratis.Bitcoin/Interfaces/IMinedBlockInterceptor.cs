using System;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    /// <summary>
    /// Allow the manipulation of a mined block before it gets added to the chain.
    /// </summary>
    public interface IMinedBlockInterceptor
    {
        /// <summary>
        /// Called when a block is mined.
        /// Allow manipulating the block before it's attached to the chain.
        /// </summary>
        /// <param name="block">The block.</param>
        void OnMinedBlock(Block block);
    }
}

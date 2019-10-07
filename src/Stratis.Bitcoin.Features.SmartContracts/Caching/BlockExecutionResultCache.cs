using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Caching
{
    public interface IBlockExecutionResultCache
    {
        /// <summary>
        /// Get all the execution results for a mined block.
        ///
        /// Returns null if the block does not exist in the cache, i.e. it was not mined on this node.
        /// </summary>
        BlockExecutionResultModel GetExecutionResult(uint256 blockHash);

        /// <summary>
        /// Store all the execution results for a mined block.
        /// </summary>
        void StoreExecutionResult(uint256 blockHash, BlockExecutionResultModel result);
    }

    public class BlockExecutionResultCache : IBlockExecutionResultCache
    {
        private Dictionary<uint256, BlockExecutionResultModel> cachedExecutions;

        public BlockExecutionResultCache()
        {
            this.cachedExecutions = new Dictionary<uint256, BlockExecutionResultModel>();
        }

        /// <inheritdoc />
        public BlockExecutionResultModel GetExecutionResult(uint256 blockHash)
        {
            this.cachedExecutions.TryGetValue(blockHash, out BlockExecutionResultModel ret);
            return ret;
        }

        /// <inheritdoc />
        public void StoreExecutionResult(uint256 blockHash, BlockExecutionResultModel result)
        {
            this.cachedExecutions.Add(blockHash, result);
        }
    }
}

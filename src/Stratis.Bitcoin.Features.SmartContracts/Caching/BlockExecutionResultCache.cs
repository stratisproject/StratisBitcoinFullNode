using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Caching
{
    public interface IBlockExecutionResultCache
    {
        /// <summary>
        /// Get all the execution results for a mined block.
        ///
        /// Returns null if 
        /// </summary>
        BlockExecutionResultModel GetExecutionResult(uint256 blockHash);

        void StoreExecutionResult(uint256 blockHash, BlockExecutionResultModel result);
    }

    public class BlockExecutionResultCache : IBlockExecutionResultCache
    {
        private Dictionary<uint256, BlockExecutionResultModel> cachedExecutions;

        public BlockExecutionResultCache()
        {
            this.cachedExecutions = new Dictionary<uint256, BlockExecutionResultModel>();
        }

        public BlockExecutionResultModel GetExecutionResult(uint256 blockHash)
        {
            BlockExecutionResultModel ret = null;
            this.cachedExecutions.TryGetValue(blockHash, out ret);
            return ret;
        }

        public void StoreExecutionResult(uint256 blockHash, BlockExecutionResultModel result)
        {
            this.cachedExecutions.Add(blockHash, result);
        }
    }
}

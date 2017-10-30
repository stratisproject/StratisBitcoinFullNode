using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Indexer.IndexTasks
{
    public interface IBlocksRepository
    {
        IEnumerable<Block> GetBlocks(IEnumerable<uint256> hashes, CancellationToken cancellationToken);
    }

    public class NodeBlocksRepository : IBlocksRepository
    {
        Node _Node;
        public NodeBlocksRepository(Node node)
        {
            _Node = node;
        }
        #region IBlocksRepository Members

        public IEnumerable<Block> GetBlocks(IEnumerable<uint256> hashes, CancellationToken cancellationToken)
        {
            return _Node.GetBlocks(hashes, cancellationToken);
        }

        #endregion
    }
}

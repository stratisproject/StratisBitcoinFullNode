using NBitcoin;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Threading;

namespace Stratis.Bitcoin.Features.AzureIndexer.IndexTasks
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

    public class FullNodeBlocksRepository : IBlocksRepository
    {
        FullNode _Node;
        BlockStore.BlockRepository _Repo;

        public FullNodeBlocksRepository(FullNode node)
        {
            _Node = node;
            _Repo = node.NodeService<BlockStore.IBlockRepository>() as BlockStore.BlockRepository;
        }

        #region IBlocksRepository Members

        public IEnumerable<Block> GetBlocks(IEnumerable<uint256> hashes, CancellationToken cancellationToken)
        {
            foreach (var hash in hashes)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (hash == _Node.Network.GenesisHash)
                    yield return _Node.Network.GetGenesis();
                else
                    yield return _Repo.GetAsync(hash).GetAwaiter().GetResult();
            }
        }

        #endregion
    }
}

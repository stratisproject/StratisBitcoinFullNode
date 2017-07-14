using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreManager
    {
        private readonly ConcurrentChain chain;
        private readonly IConnectionManager connection;
        public IndexRepository IndexRepository { get; } // public for testing
        public IndexStoreLoop IndexStoreLoop { get; } // public for testing

        private readonly IDateTimeProvider dateTimeProvider;
        private readonly NodeSettings nodeArgs;
        public ChainState ChainState { get; }

        public IndexStoreManager(ConcurrentChain chain, IConnectionManager connection, IndexRepository indexRepository,
            IDateTimeProvider dateTimeProvider, NodeSettings nodeArgs, ChainState chainState, IndexStoreLoop indexStoreLoop)
        {
            this.chain = chain;
            this.connection = connection;
            this.IndexRepository = indexRepository;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeArgs = nodeArgs;
            this.ChainState = chainState;
            this.IndexStoreLoop = indexStoreLoop;
        }
    }
}

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
{
    /// <summary>
    /// Base test class for all the BlockStoreLoop tests.
    /// </summary>
    public class BlockStoreLoopStepBaseTest : LogsTestBase
    {
        internal void AddBlockToPendingStorage(BlockStoreLoop blockStoreLoop, Block block)
        {
            var chainedBlock = blockStoreLoop.Chain.GetBlock(block.GetHash());
            blockStoreLoop.PendingStorage.TryAdd(block.GetHash(), new BlockPair(block, chainedBlock));
        }
    }

    internal class FluentBlockStoreLoop : IDisposable
    {
        private IAsyncLoopFactory asyncLoopFactory;
        private StoreBlockPuller blockPuller;
        internal IBlockRepository BlockRepository { get; private set; }
        private Mock<ChainState> chainState;
        private Mock<IConnectionManager> connectionManager;
        private DataFolder dataFolder;
        private Mock<INodeLifetime> nodeLifeTime;
        private Mock<ILoggerFactory> loggerFactory;
        private IInitialBlockDownloadState initialBlockDownloadState;

        public BlockStoreLoop Loop { get; private set; }

        internal FluentBlockStoreLoop(DataFolder dataFolder)
        {
            this.ConfigureLogger();

            this.dataFolder = dataFolder;
            this.BlockRepository = new BlockRepositoryInMemory();

            this.ConfigureConnectionManager();

            var fullNode = new Mock<FullNode>().Object;
            fullNode.DateTimeProvider = new DateTimeProvider();

            var mock = new Mock<IInitialBlockDownloadState>();
            mock.Setup(x => x.IsInitialBlockDownload()).Returns(false);
            this.initialBlockDownloadState = mock.Object;

            this.chainState = new Mock<ChainState>(new InvalidBlockHashStore(fullNode.DateTimeProvider));

            this.nodeLifeTime = new Mock<INodeLifetime>();
        }

        internal FluentBlockStoreLoop AsIBD()
        {
            var mock = new Mock<IInitialBlockDownloadState>();
            mock.Setup(x => x.IsInitialBlockDownload()).Returns(true);
            this.initialBlockDownloadState = mock.Object;
            return this;
        }

        internal FluentBlockStoreLoop WithConcreteLoopFactory()
        {
            this.asyncLoopFactory = new AsyncLoopFactory(this.loggerFactory.Object);
            return this;
        }

        internal FluentBlockStoreLoop WithConcreteRepository()
        {
            this.BlockRepository = new BlockRepository(Network.Main, this.dataFolder, DateTimeProvider.Default, this.loggerFactory.Object);
            return this;
        }

        private void ConfigureLogger()
        {
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        }

        private void ConfigureConnectionManager()
        {
            this.connectionManager = new Mock<IConnectionManager>();
            this.connectionManager.Setup(c => c.ConnectedPeers).Returns(new NetworkPeerCollection());
            this.connectionManager.Setup(c => c.NodeSettings).Returns(new NodeSettings(args:new string[] { $"-datadir={this.dataFolder.RootPath}" }));
            this.connectionManager.Setup(c => c.Parameters).Returns(new NetworkPeerConnectionParameters());
        }

        internal void Create(ConcurrentChain chain)
        {
            this.blockPuller = new StoreBlockPuller(chain, this.connectionManager.Object, this.loggerFactory.Object);

            if (this.asyncLoopFactory == null)
                this.asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;

            this.Loop = new BlockStoreLoop(
                    this.asyncLoopFactory,
                    this.blockPuller,
                    this.BlockRepository,
                    null,
                    chain,
                    this.chainState.Object,
                    new StoreSettings(new NodeSettings(args:new string[] { $"-datadir={this.dataFolder.RootPath}" })),
                    this.nodeLifeTime.Object,
                    this.loggerFactory.Object,
                    this.initialBlockDownloadState,
                    DateTimeProvider.Default);
        }

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                    this.BlockRepository.Dispose();
                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        #endregion IDisposable Support
    }
}

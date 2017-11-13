using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Tests;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
{
    /// <summary>
    /// Base test class for all the BlockStoreLoop tests.
    /// </summary>
    public class BlockStoreLoopStepBaseTest : TestBase
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
        internal BlockStore.IBlockRepository BlockRepository { get; private set; }
        private Mock<ChainState> chainState;
        private Mock<IConnectionManager> connectionManager;
        private DataFolder dataFolder;
        private Mock<INodeLifetime> nodeLifeTime;
        private Mock<ILoggerFactory> loggerFactory;

        public BlockStoreLoop Loop { get; private set; }

        internal FluentBlockStoreLoop()
        {
            this.ConfigureLogger();

            this.BlockRepository = new BlockRepositoryInMemory();
            this.dataFolder = TestBase.AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "BlockStore"));

            this.ConfigureConnectionManager();

            var fullNode = new Mock<FullNode>().Object;
            fullNode.DateTimeProvider = new DateTimeProvider();

            this.chainState = new Mock<ChainState>(fullNode);
            this.chainState.Object.SetIsInitialBlockDownload(false, DateTime.Today);

            this.nodeLifeTime = new Mock<INodeLifetime>();
        }

        internal FluentBlockStoreLoop AsIBD()
        {
            this.chainState.Object.SetIsInitialBlockDownload(true, DateTime.Today.AddDays(1));
            return this;
        }

        internal FluentBlockStoreLoop WithConcreteLoopFactory()
        {
            this.asyncLoopFactory = new AsyncLoopFactory(this.loggerFactory.Object);
            return this;
        }

        internal FluentBlockStoreLoop WithConcreteRepository(string dataFolder)
        {
            this.dataFolder = TestBase.AssureEmptyDirAsDataFolder(dataFolder);
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
            this.connectionManager.Setup(c => c.ConnectedNodes).Returns(new NodesCollection());
            this.connectionManager.Setup(c => c.NodeSettings).Returns(NodeSettings.FromArguments(new string[] { $"-datadir={this.dataFolder.WalletPath}" }));
            this.connectionManager.Setup(c => c.Parameters).Returns(new NodeConnectionParameters());
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
                    new StoreSettings(NodeSettings.FromArguments(new string[] { $"-datadir={this.dataFolder.WalletPath}" })),
                    this.nodeLifeTime.Object,
                    this.loggerFactory.Object,
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

        #endregion
    }
}
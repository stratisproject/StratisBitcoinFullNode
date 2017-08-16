using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    /// <summary>
    /// Base test class for all the BlockStoreLoop tests.
    /// </summary>
    public class BlockStoreLoopStepBaseTest
    {
        internal void AppendBlocksToChain(ConcurrentChain chain, IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (chain.Tip != null)
                    block.Header.HashPrevBlock = chain.Tip.HashBlock;
                chain.SetTip(block.Header);
            }
        }

        internal void AddBlockToPendingStorage(BlockStoreLoop blockStoreLoop, Block block)
        {
            var chainedBlock = blockStoreLoop.Chain.GetBlock(block.GetHash());
            blockStoreLoop.PendingStorage.TryAdd(block.GetHash(), new BlockPair(block, chainedBlock));
        }

        internal List<Block> CreateBlocks(int amount)
        {
            var blocks = new List<Block>();
            for (int i = 0; i < amount; i++)
            {
                Block block = CreateBlock(i);
                block.Header.HashPrevBlock = blocks.LastOrDefault()?.GetHash() ?? Network.Main.GenesisHash;
                blocks.Add(block);
            }

            return blocks;
        }

        internal Block CreateBlock(int blockNumber)
        {
            var block = new Block();

            for (int j = 0; j < 10; j++)
            {
                var trx = new Transaction();

                block.AddTransaction(new Transaction());

                trx.AddInput(new TxIn(Script.Empty));
                trx.AddOutput(Money.COIN + j + blockNumber, new Script(Enumerable.Range(1, 5).SelectMany(index => Guid.NewGuid().ToByteArray())));

                trx.AddInput(new TxIn(Script.Empty));
                trx.AddOutput(Money.COIN + j + blockNumber + 1, new Script(Enumerable.Range(1, 5).SelectMany(index => Guid.NewGuid().ToByteArray())));

                block.AddTransaction(trx);
            }

            block.UpdateMerkleRoot();

            return block;
        }
    }

    internal class FluentBlockStoreLoop : IDisposable
    {
        private IAsyncLoopFactory asyncLoopFactory;
        private StoreBlockPuller blockPuller;
        internal Features.BlockStore.IBlockRepository BlockRepository { get; private set; }
        private Mock<ChainState> chainState;
        private Mock<IConnectionManager> connectionManager;
        private DataFolder dataFolder;
        private Mock<ILogger> fullNodeLogger;
        private Mock<INodeLifetime> nodeLifeTime;
        private Mock<ILoggerFactory> loggerFactory;
        private Mock<ILogger> rpcLogger;

        public BlockStoreLoop Loop { get; private set; }

        internal FluentBlockStoreLoop()
        {
            ConfigureLogger();
            ConfigureConnectionManager();

            this.BlockRepository = new BlockRepositoryInMemory();
            this.dataFolder = TestBase.AssureEmptyDirAsDataFolder($"{AppContext.BaseDirectory}\\BlockStoreLoop");

            var fullNode = new Mock<FullNode>().Object;
            fullNode.DateTimeProvider = new DateTimeProvider();

            this.chainState = new Mock<ChainState>(fullNode);
            this.chainState.Object.SetIsInitialBlockDownload(false, DateTime.Today);

            this.nodeLifeTime = new Mock<INodeLifetime>();
        }

        internal FluentBlockStoreLoop WithConcreteLoopFactory()
        {
            this.asyncLoopFactory = new AsyncLoopFactory(this.loggerFactory.Object);
            return this;
        }

        internal FluentBlockStoreLoop WithConcreteRepository(DataFolder dataFolder)
        {
            this.BlockRepository = new BlockRepository(Network.Main, dataFolder);
            this.dataFolder = dataFolder;
            return this;
        }

        private void ConfigureLogger()
        {
            this.fullNodeLogger = new Mock<ILogger>();
            this.rpcLogger = new Mock<ILogger>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
            this.loggerFactory.Setup(l => l.CreateLogger("Stratis.Bitcoin.FullNode")).Returns(this.fullNodeLogger.Object).Verifiable();
            this.loggerFactory.Setup(l => l.CreateLogger("Stratis.Bitcoin.RPC")).Returns(this.rpcLogger.Object).Verifiable();
        }

        private void ConfigureConnectionManager()
        {
            this.connectionManager = new Mock<IConnectionManager>();
            this.connectionManager.Setup(c => c.ConnectedNodes).Returns(new NodesCollection());
            this.connectionManager.Setup(c => c.NodeSettings).Returns(NodeSettings.Default());
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
                    NodeSettings.FromArguments(new string[] { $"-datadir={dataFolder.WalletPath}" }),
                    this.nodeLifeTime.Object,
                    this.loggerFactory.Object);
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
            Dispose(true);
        }

        #endregion
    }
}
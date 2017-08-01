using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Common.Hosting;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public class BlockStoreLoopStepBaseTest
    {
        private Mock<ILogger> fullNodeLogger;
        private Mock<ILoggerFactory> loggerFactory;
        private Mock<ILogger> rpcLogger;

        private void ConfigureLogger()
        {
            this.fullNodeLogger = new Mock<ILogger>();
            this.rpcLogger = new Mock<ILogger>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
            this.loggerFactory.Setup(l => l.CreateLogger("Stratis.Bitcoin.FullNode")).Returns(this.fullNodeLogger.Object).Verifiable();
            this.loggerFactory.Setup(l => l.CreateLogger("Stratis.Bitcoin.RPC")).Returns(this.rpcLogger.Object).Verifiable();
        }

        private Mock<IConnectionManager> ConfigureConnectionManager()
        {
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectedNodes).Returns(new NodesCollection());
            connectionManager.Setup(c => c.NodeSettings).Returns(NodeSettings.Default());
            connectionManager.Setup(c => c.Parameters).Returns(new NodeConnectionParameters());
            return connectionManager;
        }

        internal void AppendBlocks(ConcurrentChain chain, IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (chain.Tip != null)
                    block.Header.HashPrevBlock = chain.Tip.HashBlock;
                chain.SetTip(block.Header);
            }
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

            for (int j = 0; j < 50; j++)
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

        internal BlockStoreLoop CreateBlockStoreLoop(ConcurrentChain chain, BlockRepository blockRepository, string testFolder)
        {
            ConfigureLogger();

            Mock<IConnectionManager> connectionManager = ConfigureConnectionManager();
            var blockPuller = new StoreBlockPuller(chain, connectionManager.Object, this.loggerFactory.Object);

            FullNode fullNode = new Mock<FullNode>().Object;
            fullNode.DateTimeProvider = new DateTimeProvider();

            var chainState = new Mock<ChainState>(fullNode);
            chainState.Object.SetIsInitialBlockDownload(false, DateTime.Today);

            IAsyncLoopFactory asyncLoopFactory = new Mock<IAsyncLoopFactory>().Object;

            INodeLifetime nodeLifeTime = new Mock<INodeLifetime>().Object;

            var blockStoreLoop = new BlockStoreLoop(
                asyncLoopFactory,
                blockPuller,
                blockRepository,
                null,
                chain,
                chainState.Object,
                NodeSettings.FromArguments(new string[] { string.Format("-datadir={0}", testFolder) }),
                nodeLifeTime,
                this.loggerFactory.Object);

            return blockStoreLoop;
        }

        internal void AddToPendingStorage(BlockStoreLoop blockStoreLoop, Block block)
        {
            ChainedBlock chainedBlock = blockStoreLoop.Chain.GetBlock(block.GetHash());
            blockStoreLoop.PendingStorage.TryAdd(block.GetHash(), new BlockPair(block, chainedBlock));
        }
    }
}
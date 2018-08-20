﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ChainRepositoryTest : TestBase
    {
        public ChainRepositoryTest() : base(KnownNetworks.StratisRegTest)
        {
        }

        [Fact]
        public async Task FinalizedHeightSavedOnDiskAsync()
        {
            string dir = CreateTestDir(this);

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.SaveFinalizedBlockHashAndHeightAsync(uint256.One, 777);
            }

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.LoadFinalizedBlockInfoAsync(new StratisMain());
                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }
        }

        [Fact]
        public async Task FinalizedHeightCantBeDecreasedAsync()
        {
            string dir = CreateTestDir(this);

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.SaveFinalizedBlockHashAndHeightAsync(uint256.One, 777);
                await repo.SaveFinalizedBlockHashAndHeightAsync(uint256.One, 555);

                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.LoadFinalizedBlockInfoAsync(new StratisMain());
                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }
        }

        [Fact]
        public void SaveWritesChainToDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ConcurrentChain(KnownNetworks.StratisRegTest);
            this.AppendBlock(chain);

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                repo.SaveAsync(chain).GetAwaiter().GetResult();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                ChainedHeader tip = null;
                foreach (Row<int, BlockHeader> row in engine.GetTransaction().SelectForward<int, BlockHeader>("Chain"))
                {
                    if (tip != null && row.Value.HashPrevBlock != tip.HashBlock)
                        break;
                    tip = new ChainedHeader(row.Value, row.Value.GetHash(), tip);
                }
                Assert.Equal(tip, chain.Tip);
            }
        }

        [Fact]
        public void GetChainReturnsConcurrentChainFromDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ConcurrentChain(KnownNetworks.StratisRegTest);
            ChainedHeader tip = this.AppendBlock(chain);

            using (var engine = new DBreezeEngine(dir))
            {
                using (DBreeze.Transactions.Transaction transaction = engine.GetTransaction())
                {
                    ChainedHeader toSave = tip;
                    var blocks = new List<ChainedHeader>();
                    while (toSave != null)
                    {
                        blocks.Insert(0, toSave);
                        toSave = toSave.Previous;
                    }

                    foreach (ChainedHeader block in blocks)
                    {
                        transaction.Insert<int, BlockHeader>("Chain", block.Height, block.Header);
                    }

                    transaction.Commit();
                }
            }
            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                var testChain = new ConcurrentChain(KnownNetworks.StratisRegTest);
                testChain.SetTip(repo.LoadAsync(testChain.Genesis).GetAwaiter().GetResult());
                Assert.Equal(tip, testChain.Tip);
            }
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.Network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chains);
        }
    }
}
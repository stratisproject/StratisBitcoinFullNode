﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBreeze;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ChainRepositoryTest : TestBase
    {
        public ChainRepositoryTest() : base(Network.StratisRegTest)
        {
        }

        [Fact]
        public async Task FinalizedHeightSavedOnDiskAsync()
        {
            string dir = CreateTestDir(this);

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.SaveFinalizedBlockHeightAsync(777);
            }

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.LoadFinalizedBlockHeightAsync();
                Assert.Equal(777, repo.GetFinalizedBlockHeight());
            }
        }

        [Fact]
        public async Task FinalizedHeightCantBeDecreasedAsync()
        {
            string dir = CreateTestDir(this);

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.SaveFinalizedBlockHeightAsync(777);
                await repo.SaveFinalizedBlockHeightAsync(555);
                
                Assert.Equal(777, repo.GetFinalizedBlockHeight());
            }

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                await repo.LoadFinalizedBlockHeightAsync();
                Assert.Equal(777, repo.GetFinalizedBlockHeight());
            }
        }

        [Fact]
        public void SaveWritesChainToDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ConcurrentChain(Network.StratisRegTest);
            this.AppendBlock(chain);

            using (var repo = new ChainRepository(dir, new LoggerFactory()))
            {
                repo.SaveAsync(chain).GetAwaiter().GetResult();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                ChainedHeader tip = null;
                foreach (var row in engine.GetTransaction().SelectForward<int, BlockHeader>("Chain"))
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
            var chain = new ConcurrentChain(Network.StratisRegTest);
            var tip = this.AppendBlock(chain);

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
                var testChain = new ConcurrentChain(Network.StratisRegTest);
                repo.LoadAsync(testChain).GetAwaiter().GetResult();
                Assert.Equal(tip, testChain.Tip);
            }
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            var nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                var block = this.Network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.Network.Consensus.ConsensusFactory.CreateTransaction());
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

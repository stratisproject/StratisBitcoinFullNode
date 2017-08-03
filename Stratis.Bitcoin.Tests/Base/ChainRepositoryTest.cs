﻿using System;
using System.Collections.Generic;
using DBreeze;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ChainRepositoryTest : TestBase
    {
        [Fact]
        public void SaveWritesChainToDisk()
        {
            string dir = AssureEmptyDir("TestData/ChainRepository/SaveWritesChainToDisk");
            var chain = new ConcurrentChain(Network.RegTest);
            this.AppendBlock(chain);

            using (var repo = new ChainRepository(dir))
            {
                repo.Save(chain).Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                ChainedBlock tip = null;
                foreach (var row in engine.GetTransaction().SelectForward<int, BlockHeader>("Chain"))
                {
                    if (tip != null && row.Value.HashPrevBlock != tip.HashBlock)
                        break;
                    tip = new ChainedBlock(row.Value, null, tip);
                }
                Assert.Equal(tip, chain.Tip);
            }
        }

        [Fact]
        public void GetChainReturnsConcurrentChainFromDisk()
        {
            string dir = AssureEmptyDir("TestData/ChainRepository/GetChainReturnsConcurrentChainFromDisk");
            var chain = new ConcurrentChain(Network.RegTest);
            var tip = this.AppendBlock(chain);

            using (var session = new DBreezeSingleThreadSession("session", dir))
            {
                session.Execute(() =>
                {
                    var toSave = tip;
                    List<ChainedBlock> blocks = new List<ChainedBlock>();
                    while (toSave != null)
                    {
                        blocks.Insert(0, toSave);
                        toSave = toSave.Previous;
                    }
                    foreach (var block in blocks)
                    {
                        session.Transaction.Insert<int, BlockHeader>("Chain", block.Height, block.Header);
                    }

                    session.Transaction.Commit();
                }).Wait();
            }

            using (var repo = new ChainRepository(dir))
            {
				var testChain = new ConcurrentChain(Network.RegTest);
                repo.Load(testChain).GetAwaiter().GetResult();
                Assert.Equal(tip, testChain.Tip);
            }
        }        

        public ChainedBlock AppendBlock(ChainedBlock previous, params ConcurrentChain[] chains)
        {
            ChainedBlock last = null;
            var nonce = RandomUtils.GetUInt32();
            foreach (var chain in chains)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedBlock AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedBlock index = null;
            return this.AppendBlock(index, chains);
        }
    }
}
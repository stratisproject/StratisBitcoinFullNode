using System;
using System.Collections.Generic;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ChainRepositoryTest : TestBase
    {
        private readonly DBreezeSerializer dBreezeSerializer;

        public ChainRepositoryTest() : base(KnownNetworks.StratisRegTest)
        {
            this.dBreezeSerializer = new DBreezeSerializer(this.Network);
        }

        [Fact]
        public void SaveWritesChainToDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ConcurrentChain(KnownNetworks.StratisRegTest);
            this.AppendBlock(chain);

            using (var repo = new ChainRepository(dir, new LoggerFactory(), this.dBreezeSerializer))
            {
                repo.SaveAsync(chain).GetAwaiter().GetResult();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                ChainedHeader tip = null;
                foreach (Row<int, byte[]> row in engine.GetTransaction().SelectForward<int, byte[]>("Chain"))
                {
                    var blockHeader = this.dBreezeSerializer.Deserialize<BlockHeader>(row.Value);

                    if (tip != null && blockHeader.HashPrevBlock != tip.HashBlock)
                        break;
                    tip = new ChainedHeader(blockHeader, blockHeader.GetHash(), tip);
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
                        transaction.Insert("Chain", block.Height, this.dBreezeSerializer.Serialize(block.Header));
                    }

                    transaction.Commit();
                }
            }
            using (var repo = new ChainRepository(dir, new LoggerFactory(), this.dBreezeSerializer))
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
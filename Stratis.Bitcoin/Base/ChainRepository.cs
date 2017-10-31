using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using DBreeze.DataTypes;
using DBreeze;

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepository : IDisposable
    {
        void Load(ConcurrentChain chain);
        void Save(ConcurrentChain chain);
    }

    public class ChainRepository : IChainRepository
    {
        private readonly DBreezeEngine dbreeze;
        private BlockLocator locator;

        public ChainRepository(string folder)
        {
            Guard.NotEmpty(folder, nameof(folder));

            this.dbreeze = new DBreezeEngine(folder);
        }

        public ChainRepository(DataFolder dataFolder)
            : this(dataFolder.ChainPath)
        {
        }

        public void Load(ConcurrentChain chain)
        {
            Guard.Assert(chain.Tip == chain.Genesis);

            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                ChainedBlock tip = null;
                bool first = true;

                foreach (Row<int, BlockHeader> row in transaction.SelectForward<int, BlockHeader>("Chain"))
                {
                    if (tip != null && row.Value.HashPrevBlock != tip.HashBlock)
                        break;

                    tip = new ChainedBlock(row.Value, null, tip);
                    if (first)
                    {
                        first = false;
                        Guard.Assert(tip.HashBlock == chain.Genesis.HashBlock); // can't swap networks
                    }
                }

                if (tip == null)
                    return;

                this.locator = tip.GetLocator();
                chain.SetTip(tip);
            }
        }

        public void Save(ConcurrentChain chain)
        {
            Guard.NotNull(chain, nameof(chain));

            using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
            {
                ChainedBlock fork = this.locator == null ? null : chain.FindFork(this.locator);
                ChainedBlock tip = chain.Tip;
                ChainedBlock toSave = tip;

                List<ChainedBlock> blocks = new List<ChainedBlock>();
                while (toSave != fork)
                {
                    blocks.Add(toSave);
                    toSave = toSave.Previous;
                }

                // DBreeze is faster on ordered insert.
                IOrderedEnumerable<ChainedBlock> orderedChainedBlocks = blocks.OrderBy(b => b.Height);
                foreach (ChainedBlock block in orderedChainedBlocks)
                {
                    transaction.Insert("Chain", block.Height, block.Header);
                }

                this.locator = tip.GetLocator();
                transaction.Commit();
            }
        }

        public void Dispose()
        {
            this.dbreeze.Dispose();
        }
    }
}

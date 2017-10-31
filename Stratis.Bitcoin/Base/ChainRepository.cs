using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using DBreeze.DataTypes;

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepository : IDisposable
    {
        Task Load(ConcurrentChain chain);
        Task Save(ConcurrentChain chain);
    }

    public class ChainRepository : IChainRepository
    {
        DBreezeSingleThreadSession session;
        private BlockLocator locator;

        public ChainRepository(string folder)
        {
            Guard.NotEmpty(folder, nameof(folder));

            this.session = new DBreezeSingleThreadSession("DBreeze ChainRepository", folder);
        }

        public ChainRepository(DataFolder dataFolder)
            : this(dataFolder.ChainPath)
        {
        }

        public Task Load(ConcurrentChain chain)
        {
            Guard.Assert(chain.Tip == chain.Genesis);

            return this.session.Execute(() =>
            {
                ChainedBlock tip = null;
                bool first = true;
                foreach (Row<int, BlockHeader> row in this.session.Transaction.SelectForward<int, BlockHeader>("Chain"))
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
            });
        }

        public Task Save(ConcurrentChain chain)
        {
            Guard.NotNull(chain, nameof(chain));

            return this.session.Execute(() =>
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

                //DBreeze faster on ordered insert
                var orderedChainedBlocks = blocks.OrderBy(b => b.Height);
                foreach (var block in orderedChainedBlocks)
                {
                    this.session.Transaction.Insert<int, BlockHeader>("Chain", block.Height, block.Header);
                }
                this.locator = tip.GetLocator();
                this.session.Transaction.Commit();
            });
        }

        public void Dispose()
        {
            this.session.Dispose();
        }
    }
}

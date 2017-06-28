using DBreeze;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.Transactions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin
{
    public interface IChainRepository : IDisposable
    {
        Task Load(ConcurrentChain chain);
        Task Save(ConcurrentChain chain);
    }

    public class ChainRepository : IChainRepository
    {
        DBreezeSingleThreadSession _Session;

        public ChainRepository(string folder)
        {
            Guard.NotEmpty(folder, nameof(folder));

            this._Session = new DBreezeSingleThreadSession("DBreeze ChainRepository", folder);
        }

        public ChainRepository(DataFolder dataFolder)
            : this(dataFolder.ChainPath)
        {
        }

        BlockLocator _Locator;
        public Task Load(ConcurrentChain chain)
        {
            Guard.Assert(chain.Tip == chain.Genesis);

            return this._Session.Do(() =>
            {
                ChainedBlock tip = null;
                bool first = true;
                foreach (var row in this._Session.Transaction.SelectForward<int, BlockHeader>("Chain"))
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
                this._Locator = tip.GetLocator();
                chain.SetTip(tip);
            });
        }

        public Task Save(ConcurrentChain chain)
        {
            Guard.NotNull(chain, nameof(chain));

            return this._Session.Do(() =>
            {
                var fork = this._Locator == null ? null : chain.FindFork(this._Locator);
                var tip = chain.Tip;
                var toSave = tip;

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
                    this._Session.Transaction.Insert<int, BlockHeader>("Chain", block.Height, block.Header);
                }
                this._Locator = tip.GetLocator();
                this._Session.Transaction.Commit();
            });
        }

        public void Dispose()
        {
            this._Session.Dispose();
        }
    }
}

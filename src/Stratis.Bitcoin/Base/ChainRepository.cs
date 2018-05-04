using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepository : IDisposable
    {
        Task LoadAsync(ConcurrentChain chain);

        Task SaveAsync(ConcurrentChain chain);
    }

    public class ChainRepository : IChainRepository
    {
        /// <summary>Access to DBreeze database.</summary>
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

        public Task LoadAsync(ConcurrentChain chain)
        {
            Guard.Assert(chain.Tip == chain.Genesis);

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    ChainedBlock tip = null;
                    Row<int, BlockHeader> firstRow = transaction.Select<int, BlockHeader>("Chain", 0);

                    if (!firstRow.Exists)
                        return;

                    BlockHeader previousHeader = firstRow.Value;
                    Guard.Assert(previousHeader.GetHash() == chain.Genesis.HashBlock); // can't swap networks

                    foreach (Row<int, BlockHeader> row in transaction.SelectForwardSkip<int, BlockHeader>("Chain", 1))
                    {
                        if ((tip != null) && (previousHeader.HashPrevBlock != tip.HashBlock))
                            break;

                        tip = new ChainedBlock(previousHeader, row.Value.HashPrevBlock, tip);
                        previousHeader = row.Value;
                    }

                    if (previousHeader != null)
                        tip = new ChainedBlock(previousHeader, previousHeader.GetHash(), tip);

                    if (tip == null)
                        return;

                    this.locator = tip.GetLocator();
                    chain.SetTip(tip);
                }
            });

            return task;
        }

        public Task SaveAsync(ConcurrentChain chain)
        {
            Guard.NotNull(chain, nameof(chain));

            Task task = Task.Run(() =>
            {
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
            });

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }
    }
}

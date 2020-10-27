using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IUtxoIndexer
    {
        ReconstructedCoinviewContext GetCoinviewAtHeight(int blockHeight);
    }

    /// <summary>
    /// This is a separate use case from the address indexer, as we need full details about the current items in the UTXO set in order to
    /// be able to potentially build transactions from them. The address indexer is also intended for long-running use cases, and does
    /// incur overhead in processing each block. This indexer, conversely, is designed for occasional usage without the requirement
    /// of persisting its output for rapid lookup.
    /// The actual coindb is not well suited to this kind of query either, as it is being accessed frequently and lacks a 'snapshot'
    /// facility. This indexer therefore trades query speed for implementation simplicity, as we only have to enumerate the entire chain
    /// without regard for significantly impacting other components.
    /// Allowing the height to be specified is an additional convenience that the coindb on its own would not give us without additional
    /// consistency safeguards, which may be invasive. For example, halting consensus at a specific height to build a snapshot.
    /// </summary>
    /// <remarks>This borrows heavily from the <see cref="Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers.BalancesController"/>, but is less specialised.</remarks>
    public class UtxoIndexer : IUtxoIndexer
    {
        private readonly Network network;
        private readonly ChainIndexer chainIndexer;
        private readonly IBlockStore blockStore;
        private readonly ILogger logger;

        public UtxoIndexer(Network network, ChainIndexer chainIndexer, IBlockStore blockStore, ILoggerFactory loggerFactory)
        {
            this.network = network;
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public ReconstructedCoinviewContext GetCoinviewAtHeight(int blockHeight)
        {
            var coinView = new ReconstructedCoinviewContext();

            // TODO: Make this a command line option
            const int batchSize = 1000;

            IEnumerable<ChainedHeader> allBlockHeaders = this.chainIndexer.EnumerateToTip(this.chainIndexer.Genesis);

            int totalBlocksCounted = 0;

            int i = 0;

            while (true)
            {
                List<ChainedHeader> headers = allBlockHeaders.Skip(i * batchSize).Take(batchSize).ToList();
                List<Block> blocks = this.blockStore.GetBlocks(headers.Select(x => x.HashBlock).ToList());

                foreach (Block block in blocks)
                {
                    this.AdjustCoinviewForBlock(block, coinView);
                    totalBlocksCounted += 1;

                    // We have reached the block height asked for.
                    if (totalBlocksCounted >= blockHeight)
                        break;
                }

                // We have seen every block up to the tip or the chosen block height.
                if (headers.Count < batchSize || totalBlocksCounted >= blockHeight)
                    break;

                // Give the user some feedback every 10k blocks
                if (i % 10 == 9)
                    this.logger.LogInformation($"Processed {totalBlocksCounted} blocks for UTXO indexing.");

                i++;
            }

            return coinView;
        }

        private void AdjustCoinviewForBlock(Block block, ReconstructedCoinviewContext coinView)
        {
            foreach (Transaction tx in block.Transactions)
            {
                // Add outputs 
                for (int i = 0; i < tx.Outputs.Count; i++)
                {
                    TxOut output = tx.Outputs[i];

                    if (output.Value <= 0 || output.ScriptPubKey.IsUnspendable)
                        continue;

                    coinView.UnspentOutputs.Add(new OutPoint(tx, i));
                    coinView.Transactions[tx.GetHash()] = tx;
                }

                // Spend inputs. Coinbases are special in that they don't reference previous transactions for their inputs, so ignore them.
                if (tx.IsCoinBase)
                    continue;

                foreach (TxIn txIn in tx.Inputs)
                {
                    // TODO: This is inefficient because the Transactions dictionary entry does not get deleted, so extra memory gets used
                    if (!coinView.UnspentOutputs.Remove(txIn.PrevOut))
                    {
                        throw new Exception("Attempted to spend a prevOut that isn't in the coinview at this time.");
                    }
                }
            }
        }
    }
}

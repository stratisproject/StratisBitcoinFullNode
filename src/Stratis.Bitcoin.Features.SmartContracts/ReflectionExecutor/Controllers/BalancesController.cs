using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    [ApiVersion("1")]
    [Route("api/[controller]")]
    public class BalancesController : Controller
    {
        private readonly ChainIndexer chainIndexer;
        private readonly IBlockStore blockStore;
        private readonly Network network;
        private readonly ILogger logger;

        public BalancesController(ChainIndexer chainIndexer,
            IBlockStore blockStore,
            Network network,
            ILoggerFactory loggerFactory)
        {
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().Name);
        }

        [Route("over-amount-at-height")]
        [HttpGet]
        public IActionResult GetBalancesOverAmount(int blockHeight, decimal amount)
        {
            Money thresholdBalance = Money.Coins(amount);

            CoinviewContext coinView = GetCoinviewAtHeight(blockHeight);
            Dictionary<Script, Money> balances = GetBalances(coinView);

            List<Script> addressesWithThreshold = balances
                .Where(x => x.Value >= thresholdBalance)
                .Select(x => x.Key)
                .ToList();

            List<string> base58Addresses = new List<string>();

            foreach (Script script in addressesWithThreshold)
            {
                GetSenderResult senderResult = this.GetAddressFromScript(script);

                if (senderResult.Success)
                {
                    base58Addresses.Add(senderResult.Sender.ToBase58Address(this.network));
                }
                else
                {
                    this.logger.LogWarning($"{script} has  a balance of {balances[script]} but is not a P2PK or P2PKH.");
                }
            }

            return Json(base58Addresses);
        }

        private CoinviewContext GetCoinviewAtHeight(int blockHeight)
        {
            var coinView = new CoinviewContext();

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

                    // We have reached the blockheight asked for
                    if (totalBlocksCounted >= blockHeight)
                        break;
                }

                // We have seen every block up to the tip or our blockheight
                if (headers.Count < batchSize || totalBlocksCounted >= blockHeight)
                    break;

                // Give the user some feedback every 10k blocks
                if (i % 10 == 9)
                    this.logger.LogInformation($"Counted {totalBlocksCounted} blocks for balance calculation.");

                i++;
            }

            return coinView;
        }

        private void AdjustCoinviewForBlock(Block block, CoinviewContext coinView)
        {
            foreach (Transaction tx in block.Transactions)
            {
                // Add outputs 
                for (int i = 0; i < tx.Outputs.Count; i++)
                {
                    TxOut output = tx.Outputs[i];

                    if (output.Value > 0)
                    {
                        coinView.UnspentOutputs.Add(new OutPoint(tx, i));
                        coinView.Transactions[tx.GetHash()] = tx;
                    }
                }

                // Spend inputs
                if (!tx.IsCoinBase)
                {
                    foreach (TxIn txIn in tx.Inputs)
                    {
                        if (!coinView.UnspentOutputs.Remove(txIn.PrevOut))
                        {
                            throw new Exception("Spending PrevOut that isn't in Coinview at this time.");
                        }
                    }
                }
            }
        }

        private Dictionary<Script, Money> GetBalances(CoinviewContext coinView)
        {
            return coinView.UnspentOutputs.Select(x => coinView.Transactions[x.Hash].Outputs[x.N]) // Get the full output data for each known unspent output.
                .GroupBy(x => x.ScriptPubKey) // Group them by Script aka address.
                .ToDictionary(x => x.Key, x => (Money)x.Sum(y => y.Value)); // Find the total value owned by this address.
        }

        private GetSenderResult GetAddressFromScript(Script script)
        {
            // Borrowed from SenderRetreiver
            PubKey payToPubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script);

            if (payToPubKey != null)
            {
                var address = new uint160(payToPubKey.Hash.ToBytes());
                return GetSenderResult.CreateSuccess(address);
            }

            if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(script))
            {
                var address = new uint160(PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(script).ToBytes());
                return GetSenderResult.CreateSuccess(address);
            }
            return GetSenderResult.CreateFailure("Addresses can only be retrieved from Pay to Pub Key or Pay to Pub Key Hash");
        }


        private class CoinviewContext
        {
            /// <summary>
            /// All of the outputs that haven't been spent at this point in time.
            /// </summary>
            public HashSet<OutPoint> UnspentOutputs { get; }

            /// <summary>
            /// Easy access to all of the loaded transactions.
            /// </summary>
            public Dictionary<uint256, Transaction> Transactions { get; }

            public CoinviewContext()
            {
                this.UnspentOutputs = new HashSet<OutPoint>();
                this.Transactions = new Dictionary<uint256, Transaction>();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests
{
    public static class CoreNodeExtensions
    {
        public static Money GetProofOfWorkRewardForMinedBlocks(this CoreNode node, int numberOfBlocks)
        {
            var coinviewRule = node.FullNode.NodeService<IConsensusRules>().GetRule<CoinViewRule>();

            int startBlock = node.FullNode.Chain.Height - numberOfBlocks + 1;

            return Enumerable.Range(startBlock, numberOfBlocks)
                .Sum(p => coinviewRule.GetProofOfWorkReward(p));
        }


        public static Money WalletBalance(this CoreNode node, string walletName)
        {
            return node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Sum(s => s.Transaction.Amount);
        }

        public static int? WalletHeight(this CoreNode node, string walletName)
        {
            return node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).First().Transaction.BlockHeight;
        }

        public static int WalletSpendableTransactionCount(this CoreNode node, string walletName)
        {
            return node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Count();
        }

        public static Money GetFee(this CoreNode node, TransactionBuildContext transactionBuildContext)
        {
            return node.FullNode.WalletTransactionHandler().EstimateFee(transactionBuildContext);
        }

        public static int GetApiPort(this CoreNode coreNode)
        {
            return coreNode.FullNode.NodeService<ApiSettings>()?.ApiPort ?? -1;
        }

        public static uint256[] GenerateBlocks(this CoreNode coreNode, int blocksToGenerate, List<Transaction> transactions = null)
        {
            var blocks = new List<Block>();

            for (int i = 0; i < blocksToGenerate; i++)
            {
                uint nonce = 0;

                var block = coreNode.FullNode.Network.Consensus.ConsensusFactory.CreateBlock();
                block.Header.HashPrevBlock = coreNode.FullNode.Chain.Tip.HashBlock;
                block.Header.Bits = block.Header.GetWorkRequired(coreNode.FullNode.Network, coreNode.FullNode.Chain.Tip);
                block.Header.UpdateTime(DateTimeOffset.UtcNow, coreNode.FullNode.Network, coreNode.FullNode.Chain.Tip);

                var coinbase = new Transaction();
                coinbase.AddInput(TxIn.CreateCoinbase(coreNode.FullNode.Chain.Height + 1));
                coinbase.AddOutput(new TxOut(coreNode.FullNode.Network.GetReward(coreNode.FullNode.Chain.Height + 1), coreNode.MinerSecret.GetAddress()));
                block.AddTransaction(coinbase);

                if (transactions?.Any() ?? false)
                {
                    transactions = Reorder(transactions);
                    block.Transactions.AddRange(transactions);
                }

                block.UpdateMerkleRoot();

                while (!block.CheckProofOfWork())
                    block.Header.Nonce = ++nonce;

                blocks.Add(block);
            }

            return blocks.Select(b => b.GetHash()).ToArray();
        }

        public static List<Transaction> Reorder(List<Transaction> transactions)
        {
            if (transactions.Count == 0)
                return transactions;

            var result = new List<Transaction>();
            Dictionary<uint256, TransactionNode> dictionary = transactions.ToDictionary(t => t.GetHash(), t => new TransactionNode(t));
            foreach (TransactionNode transaction in dictionary.Select(d => d.Value))
            {
                foreach (TxIn input in transaction.Transaction.Inputs)
                {
                    TransactionNode node = dictionary.TryGet(input.PrevOut.Hash);
                    if (node != null)
                    {
                        transaction.DependsOn.Add(node);
                    }
                }
            }

            while (dictionary.Count != 0)
            {
                foreach (TransactionNode node in dictionary.Select(d => d.Value).ToList())
                {
                    foreach (TransactionNode parent in node.DependsOn.ToList())
                    {
                        if (!dictionary.ContainsKey(parent.Hash))
                            node.DependsOn.Remove(parent);
                    }

                    if (node.DependsOn.Count == 0)
                    {
                        result.Add(node.Transaction);
                        dictionary.Remove(node.Hash);
                    }
                }
            }

            return result;
        }

        private class TransactionNode
        {
            public uint256 Hash = null;
            public Transaction Transaction = null;
            public List<TransactionNode> DependsOn = new List<TransactionNode>();

            public TransactionNode(Transaction tx)
            {
                this.Transaction = tx;
                this.Hash = tx.GetHash();
            }
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public class TestHelper
    {
        public static void WaitLoop(Func<bool> act, string failureReason = "Unknown Reason", int retryDelayInMiliseconds = 1000, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken = cancellationToken == default(CancellationToken)
                ? new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 60 * 1000).Token
                : cancellationToken;

            while (!act())
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(retryDelayInMiliseconds);
                }
                catch (OperationCanceledException e)
                {
                    Assert.False(true, $"{failureReason}{Environment.NewLine}{e.Message}");
                }
            }
        }

        public static bool AreNodesSynced(CoreNode node1, CoreNode node2, bool ignoreMempool = false)
        {
            if (node1.FullNode.Chain.Tip.HashBlock != node2.FullNode.Chain.Tip.HashBlock)
                return false;

            if (node1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != node2.FullNode.ChainBehaviorState.ConsensusTip.HashBlock)
                return false;

            if (node1.FullNode.GetBlockStoreTip().HashBlock != node2.FullNode.GetBlockStoreTip().HashBlock)
                return false;

            if (!ignoreMempool)
            {
                if (node1.FullNode.MempoolManager().InfoAll().Count != node2.FullNode.MempoolManager().InfoAll().Count)
                    return false;
            }

            if ((node1.FullNode.WalletManager().ContainsWallets) && (node2.FullNode.WalletManager().ContainsWallets))
                if (node1.FullNode.WalletManager().WalletTipHash != node2.FullNode.WalletManager().WalletTipHash)
                    return false;

            if (node1.CreateRPCClient().GetBestBlockHash() != node2.CreateRPCClient().GetBestBlockHash())
                return false;

            return true;
        }

        public static bool IsNodeSynced(CoreNode node)
        {
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.ChainBehaviorState.ConsensusTip.HashBlock)
                return false;

            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.GetBlockStoreTip().HashBlock)
                return false;

            if ((node.FullNode.WalletManager().ContainsWallets) &&
                (node.FullNode.Chain.Tip.HashBlock != node.FullNode.WalletManager().WalletTipHash))
                return false;

            return true;
        }

        public static void TriggerSync(CoreNode node)
        {
            foreach (INetworkPeer connectedPeer in node.FullNode.ConnectionManager.ConnectedPeers)
                connectedPeer.Behavior<ConsensusManagerBehavior>().ResyncAsync().GetAwaiter().GetResult();
        }

        public static bool IsNodeConnected(CoreNode node)
        {
            return node.FullNode.ConnectionManager.ConnectedPeers.Any(p => p.IsConnected);
        }

        public static void WaitForNodeToSync(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(n => WaitLoop(() => IsNodeSynced(n)));
            nodes.Skip(1).ToList().ForEach(n => WaitLoop(() => AreNodesSynced(nodes.First(), n)));
        }

        public static void WaitForNodeToSyncIgnoreMempool(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(node => WaitLoop(() => IsNodeSynced(node)));
            nodes.Skip(1).ToList().ForEach(node => WaitLoop(() => AreNodesSynced(nodes.First(), node, true)));
        }

        public static (HdAddress AddressUsed, List<uint256> BlockHashes) MineBlocks(CoreNode node, string walletName, string walletPassword, string accountName, int numberOfBlocks)
        {
            Guard.NotNull(node, nameof(node));
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(walletPassword, nameof(walletPassword));
            Guard.NotEmpty(accountName, nameof(accountName));

            if (numberOfBlocks == 0) throw new ArgumentOutOfRangeException(nameof(numberOfBlocks), "Number of blocks must be greater than zero.");

            HdAddress address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));

            Wallet wallet = node.FullNode.WalletManager().GetWalletByName(walletName);
            Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            var blockHashes = node.GenerateStratisWithMiner((int)numberOfBlocks);

            WaitForNodeToSync(node);

            return (address, blockHashes);
        }

        /// <summary>
        /// This should only be used if we need to create a block manually, in all other cases please use
        /// <see cref="CoreNode.GenerateStratisWithMiner"/>
        /// </summary>
        /// <param name="coreNode">The node we want to create the block with.</param>
        /// <param name="transactions">Transactions we want to manually include in the block.</param>
        /// <param name="nonce">Optional nonce.</param>
        public static Block GenerateBlockManually(CoreNode coreNode, List<Transaction> transactions, uint nonce = 0)
        {
            var block = coreNode.FullNode.Network.CreateBlock();
            block.Header.HashPrevBlock = coreNode.FullNode.Chain.Tip.HashBlock;
            block.Header.Bits = block.Header.GetWorkRequired(coreNode.FullNode.Network, coreNode.FullNode.Chain.Tip);
            block.Header.UpdateTime(DateTimeOffset.UtcNow, coreNode.FullNode.Network, coreNode.FullNode.Chain.Tip);

            var coinbase = coreNode.FullNode.Network.CreateTransaction();
            coinbase.AddInput(TxIn.CreateCoinbase(coreNode.FullNode.Chain.Height + 1));
            coinbase.AddOutput(new TxOut(coreNode.FullNode.Network.GetReward(coreNode.FullNode.Chain.Height + 1), coreNode.MinerSecret.GetAddress()));
            block.AddTransaction(coinbase);

            if (transactions.Any())
            {
                transactions = Reorder(transactions);
                block.Transactions.AddRange(transactions);
            }

            block.UpdateMerkleRoot();

            while (!block.CheckProofOfWork())
                block.Header.Nonce = ++nonce;

            // This will set the block size.
            block = Block.Load(block.ToBytes(), coreNode.FullNode.Network);

            coreNode.FullNode.ConsensusManager().BlockMinedAsync(block).GetAwaiter().GetResult();

            return block;
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

        public static void Disconnect(CoreNode from, CoreNode to)
        {
            from.CreateRPCClient().RemoveNode(to.Endpoint);
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

        public static TransactionBuildContext CreateTransactionBuildContext(
            Network network,
            string sendingWalletName,
            string sendingAccountName,
            string sendingPassword,
            ICollection<Recipient> recipients,
            FeeType feeType,
            int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = new WalletAccountReference(sendingWalletName, sendingAccountName),
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = sendingPassword,
                Recipients = recipients.ToList()
            };
        }

        public static void Connect(CoreNode from, CoreNode to)
        {
            from.CreateRPCClient().AddNode(to.Endpoint, true);
        }

        public static void ConnectAndSync(CoreNode from, CoreNode to)
        {
            Connect(from, to);
            WaitLoop(() => AreNodesSynced(from, to));
        }

        public static bool IsNodeConnectedTo(CoreNode thisNode, CoreNode isConnectedToNode)
        {
            return thisNode.FullNode.ConnectionManager.ConnectedPeers.Any(p => p.PeerEndPoint == isConnectedToNode.Endpoint);
        }
    }
}

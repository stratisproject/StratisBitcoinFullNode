using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class StratisMinerTests
    {
        [Fact]
        public void CanMineStratisCoins()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode node1 = builder.CreateStratisPosNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UseStratisConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddRPC();
                });

                node1.NotInIBD();

                // Create the originating node's wallet
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("Source1", "source");

                var wallet1 = wm1.GetWalletByName("source");
                var account1 = wallet1.GetAccountsByCoinType((CoinType)node1.FullNode.Network.Consensus.CoinType).First();
                var address1 = account1.GetFirstUnusedReceivingAddress();
                var secret1 = wallet1.GetExtendedPrivateKeyForAddress("Source1", address1);

                // We can use SetDummyMinerSecret here because the private key is already in the wallet
                node1.SetDummyMinerSecret(new BitcoinSecret(secret1.PrivateKey, node1.FullNode.Network));

                // Generate a block so we have some funds to create a transaction with
                node1.GenerateStratisWithMiner(1);

                Assert.True(account1.GetSpendableAmount().ConfirmedAmount > 0);

                node1.Kill();
            }
        }
    }
}
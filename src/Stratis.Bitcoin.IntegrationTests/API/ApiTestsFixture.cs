using System;
using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public class ApiTestsFixture : IDisposable
    {
        public NodeBuilder builder;
        public CoreNode stratisPowNode;
        public CoreNode stratisStakeNode;
        private bool initialBlockSignature;

        public ApiTestsFixture()
        {
            this.initialBlockSignature = Block.BlockSignature;
            Block.BlockSignature = false;

            this.builder = NodeBuilder.Create();

            this.stratisPowNode = this.builder.CreateStratisPowNode(false, fullNodeBuilder =>
            {
                FullNodeBuilderWalletExtension.UseWallet(fullNodeBuilder
                        .UsePowConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .AddMining())
                    .UseApi()
                    .AddRPC();
            });

            // start api on different ports
            this.stratisPowNode.ConfigParameters.Add("apiuri", "http://localhost:37221");
            this.builder.StartAll();

            // Move a wallet file to the right folder and restart the wallet manager to take it into account.
            this.InitializeTestWallet(this.stratisPowNode.FullNode.DataFolder.WalletPath);
            var walletManager = this.stratisPowNode.FullNode.NodeService<IWalletManager>() as WalletManager;
            walletManager.Start();

            Block.BlockSignature = true;

            this.stratisStakeNode = this.builder.CreateStratisPosNode(false, fullNodeBuilder =>
            {
                ApiFeatureExtension.UseApi(fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining())
                    .AddRPC();
            });

            this.stratisStakeNode.ConfigParameters.Add("apiuri", "http://localhost:37222");

            this.builder.StartAll();
        }

        // note: do not call this dispose in the class itself xunit will handle it.
        public void Dispose()
        {
            this.builder.Dispose();
            Block.BlockSignature = this.initialBlockSignature;
        }

        /// <summary>
        /// Copies the test wallet into data folder for node if it isnt' already present.
        /// </summary>
        /// <param name="path">The path of the folder to move the wallet to.</param>
        public void InitializeTestWallet(string path)
        {
            string testWalletPath = Path.Combine(path, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);
        }
    }
}
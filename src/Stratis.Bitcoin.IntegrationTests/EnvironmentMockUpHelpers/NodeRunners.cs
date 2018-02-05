using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public class BitcoinCoreRunner : INodeRunner
    {
        private string bitcoinD;

        public BitcoinCoreRunner(string bitcoinD)
        {
            this.bitcoinD = bitcoinD;
        }

        private Process process;

        public bool HasExited
        {
            get { return this.process == null && this.process.HasExited; }
        }

        public void Kill()
        {
            if (!this.HasExited)
            {
                this.process.Kill();
                this.process.WaitForExit();
            }
        }

        public void Start(string dataDir)
        {
            this.process = Process.Start(new FileInfo(this.bitcoinD).FullName,
                $"-conf=bitcoin.conf -datadir={dataDir} -debug=net");
        }
    }

    public class StratisBitcoinPosRunner : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;

        public StratisBitcoinPosRunner(Action<IFullNodeBuilder> callback = null)
        {
            this.callback = callback;
        }

        public bool HasExited
        {
            get { return this.FullNode.HasExited; }
        }

        public void Kill()
        {
            if (this.FullNode != null)
            {
                this.FullNode.Dispose();
            }
        }

        public void Start(string dataDir)
        {
            NodeSettings nodeSettings = new NodeSettings(InitStratisRegTest(), ProtocolVersion.ALT_PROTOCOL_VERSION, args:new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration:false);

            var node = BuildFullNode(nodeSettings, this.callback);

            this.FullNode = node;
            this.FullNode.Start();
        }

        public static FullNode BuildFullNode(NodeSettings args, Action<IFullNodeBuilder> callback = null)
        {
            FullNode node;

            if (callback != null)
            {
                var builder = new FullNodeBuilder().UseNodeSettings(args);

                callback(builder);

                node = (FullNode)builder.Build();
            }
            else
            {
                node = (FullNode)new FullNodeBuilder()
                    .UseNodeSettings(args)
                    .UseStratisConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC()
                    .MockIBD()
                    .Build();
            }

            return node;
        }

        public FullNode FullNode;

        private static Network InitStratisRegTest()
        {
            // TODO: move this to Networks
            var net = Network.GetNetwork("StratisRegTest");
            if (net != null)
                return net;

            Block.BlockSignature = true;
            Transaction.TimeStamp = true;

            var consensus = Network.StratisTest.Consensus.Clone();
            consensus.PowLimit = new Target(uint256.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var pchMessageStart = new byte[4];
            pchMessageStart[0] = 0xcd;
            pchMessageStart[1] = 0xf2;
            pchMessageStart[2] = 0xc0;
            pchMessageStart[3] = 0xef;
            var magic = BitConverter.ToUInt32(pchMessageStart, 0); //0x5223570;

            var genesis = Network.StratisMain.GetGenesis().Clone();
            genesis.Header.Time = 1494909211;
            genesis.Header.Nonce = 2433759;
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash();

            Guard.Assert(consensus.HashGenesisBlock == uint256.Parse("0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f"));

            var builder = new NetworkBuilder()
                .SetName("StratisRegTest")
                .SetConsensus(consensus)
                .SetMagic(magic)
                .SetGenesis(genesis)
                .SetPort(18444)
                .SetRPCPort(18442)
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (65) })
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) });

            return builder.BuildAndRegister();
        }

        /// <summary>
        /// Builds a node with POS miner and RPC enabled.
        /// </summary>
        /// <param name="dir">Data directory that the node should use.</param>
        /// <returns>Interface to the newly built node.</returns>
        /// <remarks>Currently the node built here does not actually stake as it has no coins in the wallet,
        /// but all the features required for it are enabled.</remarks>
        public static IFullNode BuildStakingNode(string dir, bool staking = true)
        {
            NodeSettings nodeSettings = new NodeSettings(args:new string[] { $"-datadir={dir}", $"-stake={(staking ? 1 : 0)}", "-walletname=dummy", "-walletpassword=dummy" }, loadConfiguration:false);
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseStratisConsensus()
                .UseBlockStore()
                .UseMempool()
                .UseWallet()
                .AddPowPosMining()
                .AddRPC()
                .MockIBD()
                .Build();

            return fullNode;
        }
    }

    public class StratisBitcoinPowRunner : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;

        public StratisBitcoinPowRunner(Action<IFullNodeBuilder> callback = null) : base()
        {
            this.callback = callback;
        }

        public bool HasExited
        {
            get { return this.FullNode.HasExited; }
        }

        public void Kill()
        {
            if (this.FullNode != null)
            {
                this.FullNode.Dispose();
            }
        }

        public void Start(string dataDir)
        {
            NodeSettings nodeSettings = new NodeSettings(args:new string[] { "-conf=bitcoin.conf", "-datadir=" + dataDir }, loadConfiguration:false);

            var node = BuildFullNode(nodeSettings, this.callback);

            this.FullNode = node;
            this.FullNode.Start();
        }

        public static FullNode BuildFullNode(NodeSettings args, Action<IFullNodeBuilder> callback = null)
        {
            FullNode node;

            if (callback != null)
            {
                var builder = new FullNodeBuilder().UseNodeSettings(args);

                callback(builder);

                node = (FullNode)builder.Build();
            }
            else
            {
                node = (FullNode)new FullNodeBuilder()
                    .UseNodeSettings(args)
                    .UseConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddMining()
                    .UseWallet()
                    .AddRPC()
                    .MockIBD()
                    .Build();
            }

            return node;
        }

        public FullNode FullNode;
    }

    public static class FullNodeTestBuilderExtension
    {
        public static IFullNodeBuilder MockIBD(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        // Get default IBD implementation and replace it with the mock.
                        ServiceDescriptor ibdService = services.FirstOrDefault(x => x.ServiceType == typeof(IInitialBlockDownloadState));

                        if (ibdService != null)
                        {
                            services.Remove(ibdService);
                            services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadStateMock>();
                        }
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}

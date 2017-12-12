using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.IntegrationTests
{
    internal static class FullNodeExt
    {
        public static WalletManager WalletManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletManager>() as WalletManager;
        }

        public static WalletTransactionHandler WalletTransactionHandler(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
        }

        public static ConsensusLoop ConsensusLoop(this FullNode fullNode)
        {
            return fullNode.NodeService<ConsensusLoop>();
        }

        public static CoinView CoinView(this FullNode fullNode)
        {
            return fullNode.NodeService<CoinView>();
        }

        public static MempoolManager MempoolManager(this FullNode fullNode)
        {
            return fullNode.NodeService<MempoolManager>();
        }

        public static BlockStoreManager BlockStoreManager(this FullNode fullNode)
        {
            return fullNode.NodeService<BlockStoreManager>();
        }

        public static ChainedBlock HighestPersistedBlock(this FullNode fullNode)
        {
            return (fullNode.NodeService<IBlockRepository>() as BlockRepository).HighestPersistedBlock;
        }
    }

    public enum CoreNodeState
    {
        Stopped,
        Starting,
        Running,
        Killed
    }

    public interface INodeRunner
    {
        bool HasExited { get; }

        void Kill();

        void Start(string dataDir);
    }

    public class StratisBitcoinPosRunner : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;

        public StratisBitcoinPosRunner(Action<IFullNodeBuilder> callback = null) : base()
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
            NodeSettings nodeSettings = new NodeSettings("stratis", InitStratisRegTest(), ProtocolVersion.ALT_PROTOCOL_VERSION).LoadArguments(new string[] { "-conf=stratis.conf", "-datadir=" + dataDir });

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
            NodeSettings nodeSettings = new NodeSettings().LoadArguments(new string[] { $"-datadir={dir}", $"-stake={(staking ? 1 : 0)}", "-walletname=dummy", "-walletpassword=dummy" });
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseStratisConsensus()
                .UseBlockStore()
                .UseMempool()
                .UseWallet()
                .AddPowPosMining()
                .AddRPC()
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
            NodeSettings nodeSettings = new NodeSettings().LoadArguments(new string[] { "-conf=bitcoin.conf", "-datadir=" + dataDir });

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
                    .Build();
            }

            return node;
        }

        public FullNode FullNode;
    }

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
                "-conf=bitcoin.conf" + " -datadir=" + dataDir + " -debug=net");
        }
    }

    public class NodeConfigParameters : Dictionary<string, string>
    {
        public void Import(NodeConfigParameters configParameters)
        {
            foreach (var kv in configParameters)
            {
                if (!this.ContainsKey(kv.Key))
                    this.Add(kv.Key, kv.Value);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var kv in this)
                builder.AppendLine(kv.Key + "=" + kv.Value);
            return builder.ToString();
        }
    }

    public class NodeBuilder : IDisposable
    {
        /// <summary>
        /// Deletes test folders. Stops "bitcoind" if required.
        /// </summary>
        /// <param name="folder">The folder to remove.</param>
        /// <param name="tryKill">If set to true will try to stop "bitcoind" if running.</param>
        /// <returns>Returns true if the folder was successfully removed and false otherwise.</returns>
        public static bool CleanupTestFolder(string folder, bool tryKill = true)
        {
            for (int retry = 0; retry < 2; retry++)
            {
                try
                {
                    Directory.Delete(folder, true);
                    return true;
                }
                catch (DirectoryNotFoundException)
                {
                    return true;
                }
                catch (Exception)
                {
                }

                if (tryKill)
                {
                    tryKill = false;

                    foreach (var bitcoind in Process.GetProcessesByName("bitcoind"))
                        if (bitcoind.MainModule.FileName.Contains("Stratis.Bitcoin.IntegrationTests"))
                            bitcoind.Kill();

                    Thread.Sleep(1000);
                }
            }

            return false;
        }

        public static NodeBuilder Create([CallerMemberName] string caller = null, string version = "0.13.1")
        {
            Directory.CreateDirectory("TestData");
            var path = EnsureDownloaded(version);
            caller = Path.Combine("TestData", caller);
            CleanupTestFolder(caller);
            Directory.CreateDirectory(caller);
            return new NodeBuilder(caller, path);
        }

        public void SyncNodes()
        {
            foreach (var node in this.Nodes)
            {
                foreach (var node2 in this.Nodes)
                {
                    if (node != node2)
                        node.Sync(node2, true);
                }
            }
        }

        private static string EnsureDownloaded(string version)
        {
            //is a file
            if (version.Length >= 2 && version[1] == ':')
            {
                return version;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var bitcoind = string.Format("TestData/bitcoin-{0}/bin/bitcoind.exe", version);
                if (File.Exists(bitcoind))
                    return bitcoind;
                var zip = string.Format("TestData/bitcoin-{0}-win32.zip", version);
                string url = string.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10.0);
                var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(zip, data);
                ZipFile.ExtractToDirectory(zip, new FileInfo(zip).Directory.FullName);
                return bitcoind;
            }
            else
            {
                string bitcoind = string.Format("TestData/bitcoin-{0}/bin/bitcoind", version);
                if (File.Exists(bitcoind))
                    return bitcoind;

                var zip = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? string.Format("TestData/bitcoin-{0}-x86_64-linux-gnu.tar.gz", version)
                    : string.Format("TestData/bitcoin-{0}-osx64.tar.gz", version);

                string url = string.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10.0);
                var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(zip, data);
                Process.Start("tar", "-zxvf " + zip + " -C TestData");
                return bitcoind;
            }
        }

        private int last = 0;
        private string root;

        public NodeBuilder(string root, string bitcoindPath)
        {
            this.root = root;
            this.BitcoinD = bitcoindPath;
        }

        public string BitcoinD { get; }

        public List<CoreNode> Nodes { get; } = new List<CoreNode>();

        public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

        public CoreNode CreateNode(bool start = false)
        {
            string child = this.CreateNewEmptyFolder();
            var node = new CoreNode(child, new BitcoinCoreRunner(this.BitcoinD), this);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPowNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            string child = this.CreateNewEmptyFolder();
            var node = new CoreNode(child, new StratisBitcoinPowRunner(callback), this);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPosNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            string child = this.CreateNewEmptyFolder();
            var node = new CoreNode(child, new StratisBitcoinPosRunner(callback), this, configfile: "stratis.conf");
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CloneStratisNode(CoreNode cloneNode)
        {
            var node = new CoreNode(cloneNode.Folder, new StratisBitcoinPowRunner(), this, false);
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        private string CreateNewEmptyFolder()
        {
            var child = Path.Combine(this.root, this.last.ToString());
            this.last++;

            CleanupTestFolder(child);

            return child;
        }

        public void StartAll()
        {
            Task.WaitAll(this.Nodes.Where(n => n.State == CoreNodeState.Stopped).Select(n => n.StartAsync()).ToArray());
        }

        public void Dispose()
        {
            foreach (var node in this.Nodes)
                node.Kill();
            foreach (var disposable in this.disposables)
                disposable.Dispose();
        }

        private List<IDisposable> disposables = new List<IDisposable>();

        internal void AddDisposable(IDisposable group)
        {
            this.disposables.Add(group);
        }
    }

    public class CoreNode
    {
        private readonly NodeBuilder builder;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        private int[] ports;
        private INodeRunner runner;
        private readonly NetworkCredential creds;
        private List<Transaction> transactions = new List<Transaction>();
        private HashSet<OutPoint> locked = new HashSet<OutPoint>();
        private Money fee = Money.Coins(0.0001m);
        private object lockObject = new object();

        public string Folder { get; }

        /// <summary>Location of the data directory for the node.</summary>
        public string DataFolder { get; }

        public IPEndPoint Endpoint { get { return new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.ports[0]); } }

        public string Config { get; }

        public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

        public CoreNode(string folder, INodeRunner runner, NodeBuilder builder, bool cleanfolders = true, string configfile = "bitcoin.conf")
        {
            this.runner = runner;
            this.builder = builder;
            this.Folder = folder;
            this.State = CoreNodeState.Stopped;
            if (cleanfolders)
                this.CleanFolder();

            Directory.CreateDirectory(folder);
            this.DataFolder = Path.Combine(folder, "data");
            Directory.CreateDirectory(this.DataFolder);
            var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
            this.creds = new NetworkCredential(pass, pass);
            this.Config = Path.Combine(this.DataFolder, configfile);
            this.ConfigParameters.Import(builder.ConfigParameters);
            this.ports = new int[2];
            this.FindPorts(this.ports);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            this.networkPeerFactory = new NetworkPeerFactory(DateTimeProvider.Default, loggerFactory);
        }

        /// <summary>Get stratis full node if possible.</summary>
        public FullNode FullNode
        {
            get
            {
                if (this.runner is StratisBitcoinPosRunner)
                    return ((StratisBitcoinPosRunner)this.runner).FullNode;

                return ((StratisBitcoinPowRunner)this.runner).FullNode;
            }
        }

        private void CleanFolder()
        {
            NodeBuilder.CleanupTestFolder(this.Folder);
        }

        public void Sync(CoreNode node, bool keepConnection = false)
        {
            var rpc = this.CreateRPCClient();
            var rpc1 = node.CreateRPCClient();
            rpc.AddNode(node.Endpoint, true);
            while (rpc.GetBestBlockHash() != rpc1.GetBestBlockHash())
            {
                Thread.Sleep(200);
            }
            if (!keepConnection)
                rpc.RemoveNode(node.Endpoint);
        }

        public CoreNodeState State { get; private set; }

        public int ProtocolPort
        {
            get { return this.ports[0]; }
        }

        public void NotInIBD()
        {
            // not in IBD
            this.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
        }

        public void Start()
        {
            this.StartAsync().Wait();
        }

        public RPCClient CreateRPCClient()
        {
            return new RPCClient(this.creds, new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"), Network.RegTest);
        }

        public RestClient CreateRESTClient()
        {
            return new RestClient(new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"));
        }

        public NetworkPeer CreateNetworkPeerClient()
        {
            return this.networkPeerFactory.CreateConnectedNetworkPeer(Network.RegTest, "127.0.0.1:" + this.ports[0].ToString());
        }

        public NetworkPeer CreateNodeClient(NetworkPeerConnectionParameters parameters)
        {
            return this.networkPeerFactory.CreateConnectedNetworkPeer(Network.RegTest, "127.0.0.1:" + this.ports[0].ToString(), parameters);
        }

        public async Task StartAsync()
        {
            NodeConfigParameters config = new NodeConfigParameters();
            config.Add("regtest", "1");
            config.Add("rest", "1");
            config.Add("server", "1");
            config.Add("txindex", "1");
            config.Add("rpcuser", this.creds.UserName);
            config.Add("rpcpassword", this.creds.Password);
            config.Add("port", this.ports[0].ToString());
            config.Add("rpcport", this.ports[1].ToString());
            config.Add("printtoconsole", "1");
            config.Add("keypool", "10");
            config.Import(this.ConfigParameters);
            File.WriteAllText(this.Config, config.ToString());
            lock (this.lockObject)
            {
                this.runner.Start(this.DataFolder);
                this.State = CoreNodeState.Starting;
            }
            while (true)
            {
                try
                {
                    await this.CreateRPCClient().GetBlockHashAsync(0);
                    this.State = CoreNodeState.Running;
                    break;
                }
                catch
                {
                }
                if (this.runner.HasExited)
                    break;
            }
        }

        private void FindPorts(int[] ports)
        {
            int i = 0;
            while (i < ports.Length)
            {
                var port = RandomUtils.GetUInt32() % 4000;
                port = port + 10000;
                if (ports.Any(p => p == port))
                    continue;
                try
                {
                    TcpListener l = new TcpListener(IPAddress.Loopback, (int)port);
                    l.Start();
                    l.Stop();
                    ports[i] = (int)port;
                    i++;
                }
                catch (SocketException)
                {
                }
            }
        }

        public Transaction GiveMoney(Script destination, Money amount, bool broadcast = true)
        {
            var rpc = this.CreateRPCClient();
            TransactionBuilder builder = new TransactionBuilder();
            builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
            builder.AddCoins(rpc.ListUnspent().Where(c => !this.locked.Contains(c.OutPoint)).Select(c => c.AsCoin()));
            builder.Send(destination, amount);
            builder.SendFees(this.fee);
            builder.SetChange(this.GetFirstSecret(rpc));
            var tx = builder.BuildTransaction(true);
            foreach (var outpoint in tx.Inputs.Select(i => i.PrevOut))
            {
                this.locked.Add(outpoint);
            }
            if (broadcast)
                this.Broadcast(tx);
            else
                this.transactions.Add(tx);
            return tx;
        }

        public void Rollback(Transaction tx)
        {
            this.transactions.Remove(tx);
            foreach (var outpoint in tx.Inputs.Select(i => i.PrevOut))
            {
                this.locked.Remove(outpoint);
            }
        }

        public void Broadcast(Transaction transaction)
        {
            using (var peer = this.CreateNetworkPeerClient())
            {
                peer.VersionHandshake();
                peer.SendMessageAsync(new InvPayload(transaction));
                peer.SendMessageAsync(new TxPayload(transaction));
                this.PingPong(peer);
            }
        }

        /// <summary>
        /// Emit a ping and wait the pong.
        /// </summary>
        /// <param name="cancellation"></param>
        /// <param name="peer"></param>
        /// <returns>Latency.</returns>
        public TimeSpan PingPong(NetworkPeer peer, CancellationToken cancellation = default(CancellationToken))
        {
            using (NetworkPeerListener listener = peer.CreateListener().OfType<PongPayload>())
            {
                var ping = new PingPayload()
                {
                    Nonce = RandomUtils.GetUInt64()
                };

                DateTimeOffset before = DateTimeOffset.UtcNow;
                peer.SendMessageAsync(ping);

                while (listener.ReceivePayload<PongPayload>(cancellation).Nonce != ping.Nonce)
                {
                }

                DateTimeOffset after = DateTimeOffset.UtcNow;

                return after - before;
            }
        }


        public void SelectMempoolTransactions()
        {
            var rpc = this.CreateRPCClient();
            var txs = rpc.GetRawMempool();
            var tasks = txs.Select(t => rpc.GetRawTransactionAsync(t)).ToArray();
            Task.WaitAll(tasks);
            this.transactions.AddRange(tasks.Select(t => t.Result).ToArray());
        }

        public void Split(Money amount, int parts)
        {
            var rpc = this.CreateRPCClient();
            TransactionBuilder builder = new TransactionBuilder();
            builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
            builder.AddCoins(rpc.ListUnspent().Select(c => c.AsCoin()));
            var secret = this.GetFirstSecret(rpc);
            foreach (var part in (amount - this.fee).Split(parts))
            {
                builder.Send(secret, part);
            }
            builder.SendFees(this.fee);
            builder.SetChange(secret);
            var tx = builder.BuildTransaction(true);
            this.Broadcast(tx);
        }

        public void Kill()
        {
            lock (this.lockObject)
            {
                this.runner.Kill();
                this.State = CoreNodeState.Killed;
            }
        }

        public DateTimeOffset? MockTime { get; set; }

        public void SetMinerSecret(BitcoinSecret secret)
        {
            this.CreateRPCClient().ImportPrivKey(secret);
            this.MinerSecret = secret;
        }

        public void SetDummyMinerSecret(BitcoinSecret secret)
        {
            this.MinerSecret = secret;
        }

        public BitcoinSecret MinerSecret { get; private set; }

        public Block[] Generate(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
        {
            var rpc = this.CreateRPCClient();
            BitcoinSecret dest = this.GetFirstSecret(rpc);
            var bestBlock = rpc.GetBestBlockHash();
            ConcurrentChain chain = null;
            List<Block> blocks = new List<Block>();
            DateTimeOffset now = this.MockTime == null ? DateTimeOffset.UtcNow : this.MockTime.Value;
#if !NOSOCKET
            using (var node = this.CreateNetworkPeerClient())
            {
                node.VersionHandshake();
                chain = bestBlock == node.Network.GenesisHash ? new ConcurrentChain(node.Network) : this.GetChain(node);
                for (int i = 0; i < blockCount; i++)
                {
                    uint nonce = 0;
                    Block block = new Block();
                    block.Header.HashPrevBlock = chain.Tip.HashBlock;
                    block.Header.Bits = block.Header.GetWorkRequired(rpc.Network, chain.Tip);
                    block.Header.UpdateTime(now, rpc.Network, chain.Tip);
                    var coinbase = new Transaction();
                    coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
                    coinbase.AddOutput(new TxOut(rpc.Network.GetReward(chain.Height + 1), dest.GetAddress()));
                    block.AddTransaction(coinbase);
                    if (includeUnbroadcasted)
                    {
                        this.transactions = this.Reorder(this.transactions);
                        block.Transactions.AddRange(this.transactions);
                        this.transactions.Clear();
                    }
                    block.UpdateMerkleRoot();
                    while (!block.CheckProofOfWork())
                        block.Header.Nonce = ++nonce;
                    blocks.Add(block);
                    chain.SetTip(block.Header);
                }
                if (broadcast)
                    this.BroadcastBlocks(blocks.ToArray(), node);
            }
            return blocks.ToArray();
#endif
        }

        /// <summary>
        /// Get the chain of headers from the peer (thread safe).
        /// </summary>
        /// <param name="peer">Peer to get chain from.</param>
        /// <param name="hashStop">The highest block wanted.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The chain of headers.</returns>
        private ConcurrentChain GetChain(NetworkPeer peer, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ConcurrentChain chain = new ConcurrentChain(peer.Network);
            this.SynchronizeChain(peer, chain, hashStop, cancellationToken);
            return chain;
        }

        /// <summary>
        /// Synchronize a given Chain to the tip of the given node if its height is higher. (Thread safe).
        /// </summary>
        /// <param name="peer">Node to synchronize the chain for.</param>
        /// <param name="chain">The chain to synchronize.</param>
        /// <param name="hashStop">The location until which it synchronize.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private IEnumerable<ChainedBlock> SynchronizeChain(NetworkPeer peer, ChainBase chain, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ChainedBlock oldTip = chain.Tip;
            List<ChainedBlock> headers = this.GetHeadersFromFork(peer, oldTip, hashStop, cancellationToken).ToList();
            if (headers.Count == 0)
                return new ChainedBlock[0];

            ChainedBlock newTip = headers[headers.Count - 1];

            if (newTip.Height <= oldTip.Height)
                throw new ProtocolException("No tip should have been recieved older than the local one");

            foreach (ChainedBlock header in headers)
            {
                if (!header.Validate(peer.Network))
                {
                    throw new ProtocolException("A header which does not pass proof of work verification has been received");
                }
            }

            chain.SetTip(newTip);

            return headers;
        }

        private void AssertState(NetworkPeer peer, NetworkPeerState nodeState, CancellationToken cancellationToken = default(CancellationToken))
        {
            if ((nodeState == NetworkPeerState.HandShaked) && (peer.State == NetworkPeerState.Connected))
                peer.VersionHandshake(cancellationToken);

            if (nodeState != peer.State)
                throw new InvalidOperationException("Invalid Node state, needed=" + nodeState + ", current= " + this.State);
        }

        public IEnumerable<ChainedBlock> GetHeadersFromFork(NetworkPeer peer, ChainedBlock currentTip, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.AssertState(peer, NetworkPeerState.HandShaked, cancellationToken);

            using (NetworkPeerListener listener = peer.CreateListener().OfType<HeadersPayload>())
            {
                int acceptMaxReorgDepth = 0;
                while (true)
                {
                    // Get before last so, at the end, we should only receive 1 header equals to this one (so we will not have race problems with concurrent GetChains).
                    BlockLocator awaited = currentTip.Previous == null ? currentTip.GetLocator() : currentTip.Previous.GetLocator();
                    peer.SendMessageAsync(new GetHeadersPayload()
                    {
                        BlockLocators = awaited,
                        HashStop = hashStop
                    });

                    while (true)
                    {
                        bool isOurs = false;
                        HeadersPayload headers = null;

                        using (var headersCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            headersCancel.CancelAfter(TimeSpan.FromMinutes(1.0));
                            try
                            {
                                headers = listener.ReceivePayload<HeadersPayload>(headersCancel.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                acceptMaxReorgDepth += 6;
                                if (cancellationToken.IsCancellationRequested)
                                    throw;

                                // Send a new GetHeaders.
                                break;
                            }
                        }

                        // In the special case where the remote node is at height 0 as well as us, then the headers count will be 0.
                        if ((headers.Headers.Count == 0) && (peer.PeerVersion.StartHeight == 0) && (currentTip.HashBlock == peer.Network.GenesisHash))
                            yield break;

                        if ((headers.Headers.Count == 1) && (headers.Headers[0].GetHash() == currentTip.HashBlock))
                            yield break;

                        foreach (BlockHeader header in headers.Headers)
                        {
                            uint256 hash = header.GetHash();
                            if (hash == currentTip.HashBlock)
                                continue;

                            // The previous headers request timeout, this can arrive in case of big reorg.
                            if (header.HashPrevBlock != currentTip.HashBlock)
                            {
                                int reorgDepth = 0;
                                ChainedBlock tempCurrentTip = currentTip;
                                while (reorgDepth != acceptMaxReorgDepth && tempCurrentTip != null && header.HashPrevBlock != tempCurrentTip.HashBlock)
                                {
                                    reorgDepth++;
                                    tempCurrentTip = tempCurrentTip.Previous;
                                }

                                if (reorgDepth != acceptMaxReorgDepth && tempCurrentTip != null)
                                    currentTip = tempCurrentTip;
                            }

                            if (header.HashPrevBlock == currentTip.HashBlock)
                            {
                                isOurs = true;
                                currentTip = new ChainedBlock(header, hash, currentTip);

                                yield return currentTip;

                                if (currentTip.HashBlock == hashStop)
                                    yield break;
                            }
                            else break; // Not our headers, continue receive.
                        }

                        if (isOurs)
                            break;  //Go ask for next header.
                    }
                }
            }
        }

        public bool AddToStratisMempool(Transaction trx)
        {
            var fullNode = (this.runner as StratisBitcoinPowRunner).FullNode;
            var state = new MempoolValidationState(true);

            return fullNode.MempoolManager().Validator.AcceptToMemoryPool(state, trx).Result;
        }

        public List<uint256> GenerateStratisWithMiner(int blockCount)
        {
            return this.FullNode.Services.ServiceProvider.GetService<PowMining>().GenerateBlocks(new ReserveScript { reserveSfullNodecript = this.MinerSecret.ScriptPubKey }, (ulong)blockCount, uint.MaxValue);
        }

        public Block[] GenerateStratis(int blockCount, List<Transaction> passedTransactions = null, bool broadcast = true)
        {
            var fullNode = (this.runner as StratisBitcoinPowRunner).FullNode;
            BitcoinSecret dest = this.MinerSecret;
            List<Block> blocks = new List<Block>();
            DateTimeOffset now = this.MockTime == null ? DateTimeOffset.UtcNow : this.MockTime.Value;
#if !NOSOCKET

            for (int i = 0; i < blockCount; i++)
            {
                uint nonce = 0;
                Block block = new Block();
                block.Header.HashPrevBlock = fullNode.Chain.Tip.HashBlock;
                block.Header.Bits = block.Header.GetWorkRequired(fullNode.Network, fullNode.Chain.Tip);
                block.Header.UpdateTime(now, fullNode.Network, fullNode.Chain.Tip);
                var coinbase = new Transaction();
                coinbase.AddInput(TxIn.CreateCoinbase(fullNode.Chain.Height + 1));
                coinbase.AddOutput(new TxOut(fullNode.Network.GetReward(fullNode.Chain.Height + 1), dest.GetAddress()));
                block.AddTransaction(coinbase);
                if (passedTransactions?.Any() ?? false)
                {
                    passedTransactions = this.Reorder(passedTransactions);
                    block.Transactions.AddRange(passedTransactions);
                }
                block.UpdateMerkleRoot();
                while (!block.CheckProofOfWork())
                    block.Header.Nonce = ++nonce;
                blocks.Add(block);
                if (broadcast)
                {
                    uint256 blockHash = block.GetHash();
                    var newChain = new ChainedBlock(block.Header, blockHash, fullNode.Chain.Tip);
                    var oldTip = fullNode.Chain.SetTip(newChain);
                    fullNode.ConsensusLoop().Puller.InjectBlock(blockHash, new DownloadedBlock { Length = block.GetSerializedSize(), Block = block }, CancellationToken.None);

                    //try
                    //{
                    //    var blockResult = new BlockResult { Block = block };
                    //    fullNode.ConsensusLoop.AcceptBlock(blockResult);

                    //    // similar logic to what's in the full node code
                    //    if (blockResult.Error == null)
                    //    {
                    //        fullNode.ChainBehaviorState.ConsensusTip = fullNode.ConsensusLoop.Tip;
                    //        //if (fullNode.Chain.Tip.HashBlock == blockResult.ChainedBlock.HashBlock)
                    //        //{
                    //        //    var unused = cache.FlushAsync();
                    //        //}
                    //        fullNode.Signals.Blocks.Broadcast(block);
                    //    }
                    //}
                    //catch (ConsensusErrorException)
                    //{
                    //    // set back the old tip
                    //    fullNode.Chain.SetTip(oldTip);
                    //}
                }
            }

            return blocks.ToArray();
#endif
        }

        public void BroadcastBlocks(Block[] blocks)
        {
            using (var node = this.CreateNetworkPeerClient())
            {
                node.VersionHandshake();
                this.BroadcastBlocks(blocks, node);
            }
        }

        public void BroadcastBlocks(Block[] blocks, NetworkPeer peer)
        {
            Block lastSent = null;
            foreach (var block in blocks)
            {
                peer.SendMessageAsync(new InvPayload(block));
                peer.SendMessageAsync(new BlockPayload(block));
                lastSent = block;
            }
            this.PingPong(peer);
        }

        public Block[] FindBlock(int blockCount = 1, bool includeMempool = true)
        {
            this.SelectMempoolTransactions();
            return this.Generate(blockCount, includeMempool);
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

        private List<Transaction> Reorder(List<Transaction> transactions)
        {
            if (transactions.Count == 0)
                return transactions;

            var result = new List<Transaction>();
            var dictionary = transactions.ToDictionary(t => t.GetHash(), t => new TransactionNode(t));
            foreach (var transaction in dictionary.Select(d => d.Value))
            {
                foreach (var input in transaction.Transaction.Inputs)
                {
                    var node = dictionary.TryGet(input.PrevOut.Hash);
                    if (node != null)
                    {
                        transaction.DependsOn.Add(node);
                    }
                }
            }

            while (dictionary.Count != 0)
            {
                foreach (var node in dictionary.Select(d => d.Value).ToList())
                {
                    foreach (var parent in node.DependsOn.ToList())
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

        private BitcoinSecret GetFirstSecret(RPCClient rpc)
        {
            if (this.MinerSecret != null)
                return this.MinerSecret;

            var dest = rpc.ListSecrets().FirstOrDefault();
            if (dest == null)
            {
                var address = rpc.GetNewAddress();
                dest = rpc.DumpPrivKey(address);
            }
            return dest;
        }
    }
}

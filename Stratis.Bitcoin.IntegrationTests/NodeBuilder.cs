using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.IndexStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
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
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.IntegrationTests
{
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

    public class StratisBitcoinRunner : INodeRunner
    {
        private HashSet<string> options;

        public StratisBitcoinRunner(HashSet<string> options = null):base()
        {
            this.options = options;
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
            var args = NodeSettings.FromArguments(new string[] {"-conf=bitcoin.conf", "-datadir=" + dataDir});

            var node = BuildFullNode(args, this.options);

            this.FullNode = node;
            this.FullNode.Start();
        }

        public static FullNode BuildFullNode(NodeSettings args, HashSet<string> options = null)
        {
            FullNode node;

            if (options != null && options.Contains("IndexStore"))
            {
                node = (FullNode)new FullNodeBuilder()
                    .UseNodeSettings(args)
                    .UseConsensus()
                    .UseBlockStore()
                    .UseIndexStore()
                    .UseMempool()
                    .AddMining()
                    .UseWallet()
                    .AddRPC()
                    .Build();
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

            var testWalletPath = Path.Combine(node.DataFolder.WalletPath, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);

            return node;
        }

        public FullNode FullNode;
    }

    public class BitcoinCoreRunner : INodeRunner
    {
        string _BitcoinD;

        public BitcoinCoreRunner(string bitcoinD)
        {
            this._BitcoinD = bitcoinD;
        }

        Process _Process;

        public bool HasExited
        {
            get { return this._Process == null && this._Process.HasExited; }
        }

        public void Kill()
        {
            if (!this.HasExited)
            {
                this._Process.Kill();
                this._Process.WaitForExit();
            }
        }

        public void Start(string dataDir)
        {
            this._Process = Process.Start(new FileInfo(this._BitcoinD).FullName,
                "-conf=bitcoin.conf" + " -datadir=" + dataDir + " -debug=net");
        }
    }

    public class NodeConfigParameters : Dictionary<string, string>
    {
        public void Import(NodeConfigParameters configParameters)
        {
            foreach (var kv in configParameters)
            {
                if (!ContainsKey(kv.Key))
                    Add(kv.Key, kv.Value);
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
        public static NodeBuilder Create([CallerMemberNameAttribute] string caller = null, string version = "0.13.1")
        {
            version = version ?? "0.13.1";
            if (!Directory.Exists("TestData"))
                Directory.CreateDirectory("TestData");
            var path = EnsureDownloaded(version);
            caller = Path.Combine("TestData", caller);
            bool retryDelete = true;
            try
            {
                Directory.Delete(caller, true);
                retryDelete = false;
            }
            catch (DirectoryNotFoundException)
            {
                retryDelete = false;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            if (retryDelete)
            {
                foreach (var bitcoind in Process.GetProcessesByName("bitcoind"))
                {
                    if (bitcoind.MainModule.FileName.Contains("Stratis.Bitcoin.Tests"))
                    {
                        bitcoind.Kill();
                    }
                }
                // kill all dotnet instances no us
                //foreach (var dotnet in Process.GetProcessesByName("dotnet"))
                //{
                //	if (Process.GetCurrentProcess().Id == dotnet.Id)
                //		continue;
                //	try
                //	{
                //		dotnet.Kill();
                //	}
                //	catch
                //	{
                //		// ignored
                //	}
                //}
                Thread.Sleep(1000);
                Directory.Delete(caller, true);
            }
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

        int last = 0;
        private string _Root;
        private string _Bitcoind;

        public NodeBuilder(string root, string bitcoindPath)
        {
            this._Root = root;
            this._Bitcoind = bitcoindPath;
        }

        public string BitcoinD
        {
            get { return this._Bitcoind; }
        }


        private readonly List<CoreNode> _Nodes = new List<CoreNode>();

        public List<CoreNode> Nodes
        {
            get { return this._Nodes; }
        }


        private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();

        public NodeConfigParameters ConfigParameters
        {
            get { return this._ConfigParameters; }
        }

        public CoreNode CreateNode(bool start = false)
        {
            string child = CreateNewEmptyFolder();
            var node = new CoreNode(child, new BitcoinCoreRunner(this._Bitcoind), this);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisNode(bool start = false, HashSet<string> options = null)
        {
            string child = CreateNewEmptyFolder();
            var node = new CoreNode(child, new StratisBitcoinRunner(options), this);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CloneStratisNode(CoreNode cloneNode)
        {
            var node = new CoreNode(cloneNode.Folder, new StratisBitcoinRunner(), this, false);
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        private string CreateNewEmptyFolder()
        {
            var child = Path.Combine(this._Root, this.last.ToString());
            this.last++;
            try
            {
                Directory.Delete(child, true);
            }
            catch (DirectoryNotFoundException)
            {
            }

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
            foreach (var disposable in this._Disposables)
                disposable.Dispose();
        }

        List<IDisposable> _Disposables = new List<IDisposable>();

        internal void AddDisposable(IDisposable group)
        {
            this._Disposables.Add(group);
        }
    }

    public class CoreNode
    {
        private readonly NodeBuilder _Builder;
        private string _Folder;

        public string Folder
        {
            get { return this._Folder; }
        }

        public IPEndPoint Endpoint
        {
            get { return new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.ports[0]); }
        }

        public string Config
        {
            get { return this._Config; }
        }

        private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();
        private string _Config;

        public NodeConfigParameters ConfigParameters
        {
            get { return this._ConfigParameters; }
        }

        public CoreNode(string folder, INodeRunner runner, NodeBuilder builder, bool cleanfolders = true)
        {
            this._Runner = runner;
            this._Builder = builder;
            this._Folder = folder;
            this._State = CoreNodeState.Stopped;
            if (cleanfolders)
                CleanFolder();
            Directory.CreateDirectory(folder);
            this.dataDir = Path.Combine(folder, "data");
            Directory.CreateDirectory(this.dataDir);
            var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
            this.creds = new NetworkCredential(pass, pass);
            this._Config = Path.Combine(this.dataDir, "bitcoin.conf");
            this.ConfigParameters.Import(builder.ConfigParameters);
            this.ports = new int[2];
            FindPorts(this.ports);
        }

        /// <summary>
        /// Get stratis full node if possible
        /// </summary>
        public FullNode FullNode
        {
            get { return ((StratisBitcoinRunner) this._Runner).FullNode; }
        }

        private void CleanFolder()
        {
            try
            {
                Directory.Delete(this._Folder, true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

#if !NOSOCKET
        public void Sync(CoreNode node, bool keepConnection = false)
        {
            var rpc = CreateRPCClient();
            var rpc1 = node.CreateRPCClient();
            rpc.AddNode(node.Endpoint, true);
            while (rpc.GetBestBlockHash() != rpc1.GetBestBlockHash())
            {
                Thread.Sleep(200);
            }
            if (!keepConnection)
                rpc.RemoveNode(node.Endpoint);
        }
#endif
        private CoreNodeState _State;

        public CoreNodeState State
        {
            get { return this._State; }
        }

        int[] ports;

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
            StartAsync().Wait();
        }

        readonly NetworkCredential creds;

        public RPCClient CreateRPCClient()
        {
            return new RPCClient(this.creds, new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"), Network.RegTest);
        }

        public RestClient CreateRESTClient()
        {
            return new RestClient(new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"));
        }

#if !NOSOCKET
        public Node CreateNodeClient()
        {
            return Node.Connect(Network.RegTest, "127.0.0.1:" + this.ports[0].ToString());
        }

        public Node CreateNodeClient(NodeConnectionParameters parameters)
        {
            return Node.Connect(Network.RegTest, "127.0.0.1:" + this.ports[0].ToString(), parameters);
        }
#endif

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
            File.WriteAllText(this._Config, config.ToString());
            lock (this.l)
            {
                this._Runner.Start(this.dataDir);
                this._State = CoreNodeState.Starting;
            }
            while (true)
            {
                try
                {
                    await CreateRPCClient().GetBlockHashAsync(0).ConfigureAwait(false);
                    this._State = CoreNodeState.Running;
                    break;
                }
                catch
                {
                }
                if (this._Runner.HasExited)
                    break;
            }
        }

        INodeRunner _Runner;

        private readonly string dataDir;

        private void FindPorts(int[] ports)
        {
            int i = 0;
            while (i < ports.Length)
            {
                var port = RandomUtils.GetUInt32()%4000;
                port = port + 10000;
                if (ports.Any(p => p == port))
                    continue;
                try
                {
                    TcpListener l = new TcpListener(IPAddress.Loopback, (int) port);
                    l.Start();
                    l.Stop();
                    ports[i] = (int) port;
                    i++;
                }
                catch (SocketException)
                {
                }
            }
        }

        List<Transaction> transactions = new List<Transaction>();
        HashSet<OutPoint> locked = new HashSet<OutPoint>();
        Money fee = Money.Coins(0.0001m);

        public Transaction GiveMoney(Script destination, Money amount, bool broadcast = true)
        {
            var rpc = CreateRPCClient();
            TransactionBuilder builder = new TransactionBuilder();
            builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
            builder.AddCoins(rpc.ListUnspent().Where(c => !this.locked.Contains(c.OutPoint)).Select(c => c.AsCoin()));
            builder.Send(destination, amount);
            builder.SendFees(this.fee);
            builder.SetChange(GetFirstSecret(rpc));
            var tx = builder.BuildTransaction(true);
            foreach (var outpoint in tx.Inputs.Select(i => i.PrevOut))
            {
                this.locked.Add(outpoint);
            }
            if (broadcast)
                Broadcast(tx);
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

#if !NOSOCKET
        public void Broadcast(Transaction transaction)
        {
            using (var node = CreateNodeClient())
            {
                node.VersionHandshake();
                node.SendMessageAsync(new InvPayload(transaction));
                node.SendMessageAsync(new TxPayload(transaction));
                node.PingPong();
            }
        }
#else
        public void Broadcast(Transaction transaction)
        {
            var rpc = CreateRPCClient();
            rpc.SendRawTransaction(transaction);
        }
#endif

        public void SelectMempoolTransactions()
        {
            var rpc = CreateRPCClient();
            var txs = rpc.GetRawMempool();
            var tasks = txs.Select(t => rpc.GetRawTransactionAsync(t)).ToArray();
            Task.WaitAll(tasks);
            this.transactions.AddRange(tasks.Select(t => t.Result).ToArray());
        }

        public void Split(Money amount, int parts)
        {
            var rpc = CreateRPCClient();
            TransactionBuilder builder = new TransactionBuilder();
            builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
            builder.AddCoins(rpc.ListUnspent().Select(c => c.AsCoin()));
            var secret = GetFirstSecret(rpc);
            foreach (var part in (amount - this.fee).Split(parts))
            {
                builder.Send(secret, part);
            }
            builder.SendFees(this.fee);
            builder.SetChange(secret);
            var tx = builder.BuildTransaction(true);
            Broadcast(tx);
        }

        object l = new object();

        public void Kill(bool cleanFolder = true)
        {
            lock (this.l)
            {
                this._Runner.Kill();
                this._State = CoreNodeState.Killed;
                if (cleanFolder)
                    CleanFolder();
            }
        }

        public DateTimeOffset? MockTime { get; set; }

        public void SetMinerSecret(BitcoinSecret secret)
        {
            CreateRPCClient().ImportPrivKey(secret);
            this.MinerSecret = secret;
        }

        public void SetDummyMinerSecret(BitcoinSecret secret)
        {
            this.MinerSecret = secret;
        }

        public BitcoinSecret MinerSecret { get; private set; }

        public Block[] Generate(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
        {
            var rpc = CreateRPCClient();
            BitcoinSecret dest = GetFirstSecret(rpc);
            var bestBlock = rpc.GetBestBlockHash();
            ConcurrentChain chain = null;
            List<Block> blocks = new List<Block>();
            DateTimeOffset now = this.MockTime == null ? DateTimeOffset.UtcNow : this.MockTime.Value;
#if !NOSOCKET
            using (var node = CreateNodeClient())
            {

                node.VersionHandshake();
                chain = bestBlock == node.Network.GenesisHash ? new ConcurrentChain(node.Network) : node.GetChain();
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
                        this.transactions = Reorder(this.transactions);
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
                    BroadcastBlocks(blocks.ToArray(), node);
            }
            return blocks.ToArray();
#endif
        }

        public bool AddToStratisMempool(Transaction trx)
        {
            var fullNode = (this._Runner as StratisBitcoinRunner).FullNode;
            var state = new MempoolValidationState(true);

            return fullNode.MempoolManager.Validator.AcceptToMemoryPool(state, trx).Result;
        }

        public List<uint256> GenerateStratisWithMiner(int blockCount)
        {
            return this.FullNode.Services.ServiceProvider.GetService<PowMining>().GenerateBlocks(new ReserveScript { reserveSfullNodecript = this.MinerSecret.ScriptPubKey }, (ulong)blockCount, uint.MaxValue);
        }

        public Block[] GenerateStratis(int blockCount, List<Transaction> passedTransactions = null, bool broadcast = true)
        {
            var fullNode = (this._Runner as StratisBitcoinRunner).FullNode;
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
                    passedTransactions = Reorder(passedTransactions);
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
                    fullNode.ConsensusLoop.Puller.InjectBlock(blockHash, new DownloadedBlock() { Length = block.GetSerializedSize(), Block = block }, CancellationToken.None);

                    //try
                    //{
                        

                    //	var blockResult = new BlockResult { Block = block };
                    //	fullNode.ConsensusLoop.AcceptBlock(blockResult);


                    //	// similar logic to what's in the full node code
                    //	if (blockResult.Error == null)
                    //	{
                    //		fullNode.ChainBehaviorState.HighestValidatedPoW = fullNode.ConsensusLoop.Tip;
                    //		//if (fullNode.Chain.Tip.HashBlock == blockResult.ChainedBlock.HashBlock)
                    //		//{
                    //		//	var unused = cache.FlushAsync();
                    //		//}
                    //		fullNode.Signals.Blocks.Broadcast(block);
                    //	}
                    //}
                    //catch (ConsensusErrorException)
                    //{
                    //	// set back the old tip
                    //	fullNode.Chain.SetTip(oldTip);
                    //}
                }
            }

            return blocks.ToArray();
#endif
        }

        public void BroadcastBlocks(Block[] blocks)
        {
            using(var node = CreateNodeClient())
            {
                node.VersionHandshake();
                BroadcastBlocks(blocks, node);
            }
        }

        public void BroadcastBlocks(Block[] blocks, Node node)
        {
            Block lastSent = null;
            foreach(var block in blocks)
            {
                node.SendMessageAsync(new InvPayload(block));
                node.SendMessageAsync(new BlockPayload(block));
                lastSent = block;
            }
            node.PingPong();
        }

        public Block[] FindBlock(int blockCount = 1, bool includeMempool = true)
        {
            SelectMempoolTransactions();
            return Generate(blockCount, includeMempool);
        }

        class TransactionNode
        {
            public TransactionNode(Transaction tx)
            {
                this.Transaction = tx;
                this.Hash = tx.GetHash();
            }
            public uint256 Hash = null;
            public Transaction Transaction = null;
            public List<TransactionNode> DependsOn = new List<TransactionNode>();
        }

        private List<Transaction> Reorder(List<Transaction> transactions)
        {
            if(transactions.Count == 0)
                return transactions;
            var result = new List<Transaction>();
            var dictionary = transactions.ToDictionary(t => t.GetHash(), t => new TransactionNode(t));
            foreach(var transaction in dictionary.Select(d => d.Value))
            {
                foreach(var input in transaction.Transaction.Inputs)
                {
                    var node = dictionary.TryGet(input.PrevOut.Hash);
                    if(node != null)
                    {
                        transaction.DependsOn.Add(node);
                    }
                }
            }
            while(dictionary.Count != 0)
            {
                foreach(var node in dictionary.Select(d => d.Value).ToList())
                {
                    foreach(var parent in node.DependsOn.ToList())
                    {
                        if(!dictionary.ContainsKey(parent.Hash))
                            node.DependsOn.Remove(parent);
                    }
                    if(node.DependsOn.Count == 0)
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
            if(this.MinerSecret != null)
                return this.MinerSecret;
            var dest = rpc.ListSecrets().FirstOrDefault();
            if(dest == null)
            {
                var address = rpc.GetNewAddress();
                dest = rpc.DumpPrivKey(address);
            }
            return dest;
        }
    }
}

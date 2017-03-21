using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Stratis.Bitcoin.Configuration;
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
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;

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
		public bool HasExited
		{
			get { return FullNode.HasExited; }
		}

		public void Kill()
		{
			FullNode.Dispose();
		}

		public void Start(string dataDir)
		{
			var args = NodeSettings.FromArguments(new string[] {"-conf=bitcoin.conf", "-datadir=" + dataDir});

			var node = BuildFullNode(args);

			FullNode = node;
			FullNode.Start();
		}

		public static FullNode BuildFullNode(NodeSettings args)
		{
			var node = (FullNode)new FullNodeBuilder()
				.UseNodeSettings(args)
				.UseBlockStore()
				.UseMempool()
				.Build();

			return node;
		}

		public FullNode FullNode;
	}

	public class BitcoinCoreRunner : INodeRunner
	{
		string _BitcoinD;

		public BitcoinCoreRunner(string bitcoinD)
		{
			_BitcoinD = bitcoinD;
		}

		Process _Process;

		public bool HasExited
		{
			get { return _Process == null && _Process.HasExited; }
		}

		public void Kill()
		{
			if (!HasExited)
			{
				_Process.Kill();
				_Process.WaitForExit();
			}
		}

		public void Start(string dataDir)
		{
			_Process = Process.Start(new FileInfo(_BitcoinD).FullName,
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
			foreach (var node in Nodes)
			{
				foreach (var node2 in Nodes)
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
				var bitcoind = String.Format("TestData/bitcoin-{0}/bin/bitcoind.exe", version);
				if (File.Exists(bitcoind))
					return bitcoind;
				var zip = String.Format("TestData/bitcoin-{0}-win32.zip", version);
				string url = String.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
				HttpClient client = new HttpClient();
				client.Timeout = TimeSpan.FromMinutes(10.0);
				var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
				File.WriteAllBytes(zip, data);
				ZipFile.ExtractToDirectory(zip, new FileInfo(zip).Directory.FullName);
				return bitcoind;
			}
			else
			{
				string bitcoind = String.Format("TestData/bitcoin-{0}/bin/bitcoind", version);
				if (File.Exists(bitcoind))
					return bitcoind;

				var zip = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
					? String.Format("TestData/bitcoin-{0}-x86_64-linux-gnu.tar.gz", version)
					: String.Format("TestData/bitcoin-{0}-osx64.tar.gz", version);

				string url = String.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
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
			get { return _Bitcoind; }
		}


		private readonly List<CoreNode> _Nodes = new List<CoreNode>();

		public List<CoreNode> Nodes
		{
			get { return _Nodes; }
		}


		private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();

		public NodeConfigParameters ConfigParameters
		{
			get { return _ConfigParameters; }
		}

		public CoreNode CreateNode(bool start = false)
		{
			string child = CreateNewEmptyFolder();
			var node = new CoreNode(child, new BitcoinCoreRunner(_Bitcoind), this);
			Nodes.Add(node);
			if (start)
				node.Start();
			return node;
		}

		public CoreNode CreateStratisNode(bool start = false)
		{
			string child = CreateNewEmptyFolder();
			var node = new CoreNode(child, new StratisBitcoinRunner(), this);
			Nodes.Add(node);
			if (start)
				node.Start();
			return node;
		}

		private string CreateNewEmptyFolder()
		{
			var child = Path.Combine(_Root, last.ToString());
			last++;
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
			Task.WaitAll(Nodes.Where(n => n.State == CoreNodeState.Stopped).Select(n => n.StartAsync()).ToArray());
		}

		public void Dispose()
		{
			foreach (var node in Nodes)
				node.Kill();
			foreach (var disposable in _Disposables)
				disposable.Dispose();
		}

		List<IDisposable> _Disposables = new List<IDisposable>();

		internal void AddDisposable(IDisposable group)
		{
			_Disposables.Add(group);
		}
	}

	public class CoreNode
	{
		private readonly NodeBuilder _Builder;
		private string _Folder;

		public string Folder
		{
			get { return _Folder; }
		}

		public IPEndPoint Endpoint
		{
			get { return new IPEndPoint(IPAddress.Parse("127.0.0.1"), ports[0]); }
		}

		public string Config
		{
			get { return _Config; }
		}

		private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();
		private string _Config;

		public NodeConfigParameters ConfigParameters
		{
			get { return _ConfigParameters; }
		}

		public CoreNode(string folder, INodeRunner runner, NodeBuilder builder)
		{
			_Runner = runner;
			this._Builder = builder;
			this._Folder = folder;
			_State = CoreNodeState.Stopped;
			CleanFolder();
			Directory.CreateDirectory(folder);
			dataDir = Path.Combine(folder, "data");
			Directory.CreateDirectory(dataDir);
			var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
			creds = new NetworkCredential(pass, pass);
			_Config = Path.Combine(dataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters);
			ports = new int[2];
			FindPorts(ports);
		}

		/// <summary>
		/// Get stratis full node if possible
		/// </summary>
		public FullNode FullNode
		{
			get { return ((StratisBitcoinRunner) _Runner).FullNode; }
		}

		private void CleanFolder()
		{
			try
			{
				Directory.Delete(_Folder, true);
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
			get { return _State; }
		}

		int[] ports;

		public int ProtocolPort
		{
			get { return ports[0]; }
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
			return new RPCClient(creds, new Uri("http://127.0.0.1:" + ports[1].ToString() + "/"), Network.RegTest);
		}

		public RestClient CreateRESTClient()
		{
			return new RestClient(new Uri("http://127.0.0.1:" + ports[1].ToString() + "/"));
		}

#if !NOSOCKET
		public Node CreateNodeClient()
		{
			return Node.Connect(Network.RegTest, "127.0.0.1:" + ports[0].ToString());
		}

		public Node CreateNodeClient(NodeConnectionParameters parameters)
		{
			return Node.Connect(Network.RegTest, "127.0.0.1:" + ports[0].ToString(), parameters);
		}
#endif

		public async Task StartAsync()
		{
			NodeConfigParameters config = new NodeConfigParameters();
			config.Add("regtest", "1");
			config.Add("rest", "1");
			config.Add("server", "1");
			config.Add("txindex", "1");
			config.Add("rpcuser", creds.UserName);
			config.Add("rpcpassword", creds.Password);
			config.Add("port", ports[0].ToString());
			config.Add("rpcport", ports[1].ToString());
			config.Add("printtoconsole", "1");
			config.Add("keypool", "10");
			config.Import(ConfigParameters);
			File.WriteAllText(_Config, config.ToString());
			lock (l)
			{
				_Runner.Start(dataDir);
				_State = CoreNodeState.Starting;
			}
			while (true)
			{
				try
				{
					await CreateRPCClient().GetBlockHashAsync(0).ConfigureAwait(false);
					_State = CoreNodeState.Running;
					break;
				}
				catch
				{
				}
				if (_Runner.HasExited)
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
			builder.AddCoins(rpc.ListUnspent().Where(c => !locked.Contains(c.OutPoint)).Select(c => c.AsCoin()));
			builder.Send(destination, amount);
			builder.SendFees(fee);
			builder.SetChange(GetFirstSecret(rpc));
			var tx = builder.BuildTransaction(true);
			foreach (var outpoint in tx.Inputs.Select(i => i.PrevOut))
			{
				locked.Add(outpoint);
			}
			if (broadcast)
				Broadcast(tx);
			else
				transactions.Add(tx);
			return tx;
		}

		public void Rollback(Transaction tx)
		{
			transactions.Remove(tx);
			foreach (var outpoint in tx.Inputs.Select(i => i.PrevOut))
			{
				locked.Remove(outpoint);
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
			transactions.AddRange(tasks.Select(t => t.Result).ToArray());
		}

		public void Split(Money amount, int parts)
		{
			var rpc = CreateRPCClient();
			TransactionBuilder builder = new TransactionBuilder();
			builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
			builder.AddCoins(rpc.ListUnspent().Select(c => c.AsCoin()));
			var secret = GetFirstSecret(rpc);
			foreach (var part in (amount - fee).Split(parts))
			{
				builder.Send(secret, part);
			}
			builder.SendFees(fee);
			builder.SetChange(secret);
			var tx = builder.BuildTransaction(true);
			Broadcast(tx);
		}

		object l = new object();

		public void Kill(bool cleanFolder = true)
		{
			lock (l)
			{
				_Runner.Kill();
				_State = CoreNodeState.Killed;
				if (cleanFolder)
					CleanFolder();
			}
		}

		public DateTimeOffset? MockTime { get; set; }

		public void SetMinerSecret(BitcoinSecret secret)
		{
			CreateRPCClient().ImportPrivKey(secret);
			MinerSecret = secret;
		}

		public void SetDummyMinerSecret(BitcoinSecret secret)
		{
			MinerSecret = secret;
		}

		public BitcoinSecret MinerSecret { get; private set; }

		public Block[] Generate(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
		{
			var rpc = CreateRPCClient();
			BitcoinSecret dest = GetFirstSecret(rpc);
			var bestBlock = rpc.GetBestBlockHash();
			ConcurrentChain chain = null;
			List<Block> blocks = new List<Block>();
			DateTimeOffset now = MockTime == null ? DateTimeOffset.UtcNow : MockTime.Value;
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
						transactions = Reorder(transactions);
						block.Transactions.AddRange(transactions);
						transactions.Clear();
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
			var fullNode = (_Runner as StratisBitcoinRunner).FullNode;
			var state = new MempoolValidationState(true);

			return fullNode.MempoolManager.Validator.AcceptToMemoryPool(state, trx).Result;
		}

		public Block[] GenerateStratis(int blockCount, List<Transaction> passedTransactions = null, bool broadcast = true)
		{
			var fullNode = (_Runner as StratisBitcoinRunner).FullNode;
			BitcoinSecret dest = MinerSecret;
			List<Block> blocks = new List<Block>();
			DateTimeOffset now = MockTime == null ? DateTimeOffset.UtcNow : MockTime.Value;
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
					var newChain = new ChainedBlock(block.Header, block.GetHash(), fullNode.Chain.Tip);
					var oldTip = fullNode.Chain.SetTip(newChain);
					fullNode.ConsensusLoop.Puller.PushBlock(block.GetSerializedSize(), block, CancellationToken.None);

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
				Transaction = tx;
				Hash = tx.GetHash();
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
			if(MinerSecret != null)
				return MinerSecret;
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

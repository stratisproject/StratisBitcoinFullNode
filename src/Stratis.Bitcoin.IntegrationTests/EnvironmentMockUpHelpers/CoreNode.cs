using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public class CoreNode
    {
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

        public CoreNode(string folder, INodeRunner runner, NodeBuilder builder, Network network, string configfile = "bitcoin.conf")
        {
            this.runner = runner;
            this.Folder = folder;
            this.DataFolder = Path.Combine(folder, "data");

            this.State = CoreNodeState.Stopped;
            var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
            this.creds = new NetworkCredential(pass, pass);
            this.Config = Path.Combine(this.DataFolder, configfile);
            this.ConfigParameters.Import(builder.ConfigParameters);
            this.ports = new int[2];
            this.FindPorts(this.ports);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            this.networkPeerFactory = new NetworkPeerFactory(network, DateTimeProvider.Default, loggerFactory, new PayloadProvider().DiscoverPayloads(), new SelfEndpointTracker());
        }

        /// <summary>Get stratis full node if possible.</summary>
        public FullNode FullNode
        {
            get
            {
                return this.runner.FullNode;
            }
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
            (this.FullNode.NodeService<IInitialBlockDownloadState>() as InitialBlockDownloadStateMock).SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
        }

        public RPCClient CreateRPCClient()
        {
            return new RPCClient(this.creds, new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"), Network.RegTest);
        }

        public RestClient CreateRESTClient()
        {
            return new RestClient(new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"));
        }

        public INetworkPeer CreateNetworkPeerClient()
        {
            return this.networkPeerFactory.CreateConnectedNetworkPeerAsync("127.0.0.1:" + this.ports[0].ToString()).GetAwaiter().GetResult();
        }

        public void Start()
        {
            NodeBuilder.CreateDataFolder(this.DataFolder);

            var config = new NodeConfigParameters();
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
            config.Add("agentprefix", "node" + this.ports[0].ToString());
            config.Import(this.ConfigParameters);
            File.WriteAllText(this.Config, config.ToString());

            lock (this.lockObject)
            {
                this.runner.Start(this.DataFolder);
                this.State = CoreNodeState.Starting;
            }

            if (this.runner is BitcoinCoreRunner)
                StartBitcoinCoreRunner();
            else
                StartStratisRunner();

            this.State = CoreNodeState.Running;
        }

        private void StartBitcoinCoreRunner()
        {
            while (true)
            {
                try
                {
                    CreateRPCClient().GetBlockHashAsync(0).GetAwaiter().GetResult();
                    this.State = CoreNodeState.Running;
                    break;
                }
                catch { }

                Task.Delay(200);
            }
        }

        private void StartStratisRunner()
        {
            while (true)
            {
                if (this.runner.FullNode == null)
                {
                    Thread.Sleep(100);
                    continue;
                }

                if (this.runner.FullNode.State == FullNodeState.Started)
                    break;
                else
                    Thread.Sleep(200);
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
            using (INetworkPeer peer = this.CreateNetworkPeerClient())
            {
                peer.VersionHandshakeAsync().GetAwaiter().GetResult();
                peer.SendMessageAsync(new InvPayload(transaction)).GetAwaiter().GetResult();
                peer.SendMessageAsync(new TxPayload(transaction)).GetAwaiter().GetResult();
                this.PingPongAsync(peer).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Emit a ping and wait the pong.
        /// </summary>
        /// <param name="cancellation"></param>
        /// <param name="peer"></param>
        /// <returns>Latency.</returns>
        public async Task<TimeSpan> PingPongAsync(INetworkPeer peer, CancellationToken cancellation = default(CancellationToken))
        {
            using (var listener = new NetworkPeerListener(peer))
            {
                var ping = new PingPayload()
                {
                    Nonce = RandomUtils.GetUInt64()
                };

                DateTimeOffset before = DateTimeOffset.UtcNow;
                await peer.SendMessageAsync(ping);

                while ((await listener.ReceivePayloadAsync<PongPayload>(cancellation).ConfigureAwait(false)).Nonce != ping.Nonce)
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

        public async Task<Block[]> GenerateAsync(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
        {
            var rpc = this.CreateRPCClient();
            BitcoinSecret dest = this.GetFirstSecret(rpc);
            var bestBlock = rpc.GetBestBlockHash();
            ConcurrentChain chain = null;
            List<Block> blocks = new List<Block>();
            DateTimeOffset now = this.MockTime == null ? DateTimeOffset.UtcNow : this.MockTime.Value;

            using (INetworkPeer peer = this.CreateNetworkPeerClient())
            {
                peer.VersionHandshakeAsync().GetAwaiter().GetResult();
                chain = bestBlock == peer.Network.GenesisHash ? new ConcurrentChain(peer.Network) : this.GetChain(peer);
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
                    while (!block.CheckProofOfWork(rpc.Network.Consensus))
                        block.Header.Nonce = ++nonce;
                    blocks.Add(block);
                    chain.SetTip(block.Header);
                }
                if (broadcast)
                    await this.BroadcastBlocksAsync(blocks.ToArray(), peer);
            }
            return blocks.ToArray();
        }

        /// <summary>
        /// Get the chain of headers from the peer (thread safe).
        /// </summary>
        /// <param name="peer">Peer to get chain from.</param>
        /// <param name="hashStop">The highest block wanted.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The chain of headers.</returns>
        private ConcurrentChain GetChain(INetworkPeer peer, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
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
        private IEnumerable<ChainedBlock> SynchronizeChain(INetworkPeer peer, ChainBase chain, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
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

        private async Task AssertStateAsync(INetworkPeer peer, NetworkPeerState peerState, CancellationToken cancellationToken = default(CancellationToken))
        {
            if ((peerState == NetworkPeerState.HandShaked) && (peer.State == NetworkPeerState.Connected))
                await peer.VersionHandshakeAsync(cancellationToken);

            if (peerState != peer.State)
                throw new InvalidOperationException("Invalid Node state, needed=" + peerState + ", current= " + this.State);
        }

        public IEnumerable<ChainedBlock> GetHeadersFromFork(INetworkPeer peer, ChainedBlock currentTip, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.AssertStateAsync(peer, NetworkPeerState.HandShaked, cancellationToken).GetAwaiter().GetResult();

            using (var listener = new NetworkPeerListener(peer))
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
                    }).GetAwaiter().GetResult();

                    while (true)
                    {
                        bool isOurs = false;
                        HeadersPayload headers = null;

                        using (var headersCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            headersCancel.CancelAfter(TimeSpan.FromMinutes(1.0));
                            try
                            {
                                headers = listener.ReceivePayloadAsync<HeadersPayload>(headersCancel.Token).GetAwaiter().GetResult();
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
            return this.FullNode.Services.ServiceProvider.GetService<IPowMining>().GenerateBlocks(new ReserveScript { ReserveFullNodeScript = this.MinerSecret.ScriptPubKey }, (ulong)blockCount, uint.MaxValue);
        }

        [Obsolete("Please use GenerateStratisWithMiner instead.")]
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
                while (!block.CheckProofOfWork(fullNode.Network.Consensus))
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

        public async Task BroadcastBlocksAsync(Block[] blocks)
        {
            using (INetworkPeer peer = this.CreateNetworkPeerClient())
            {
                await peer.VersionHandshakeAsync();
                await this.BroadcastBlocksAsync(blocks, peer);
            }
        }

        public async Task BroadcastBlocksAsync(Block[] blocks, INetworkPeer peer)
        {
            foreach (var block in blocks)
            {
                await peer.SendMessageAsync(new InvPayload(block));
                await peer.SendMessageAsync(new BlockPayload(block));
            }
            await this.PingPongAsync(peer);
        }

        public Block[] FindBlock(int blockCount = 1, bool includeMempool = true)
        {
            this.SelectMempoolTransactions();
            return this.GenerateAsync(blockCount, includeMempool).GetAwaiter().GetResult();
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

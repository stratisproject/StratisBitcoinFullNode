﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class CoreNode
    {
        private readonly NodeRunner runner;
        private readonly NetworkCredential creds;
        private List<Transaction> transactions = new List<Transaction>();
        private readonly object lockObject = new object();

        public int ProtocolPort => int.Parse(this.ConfigParameters["port"]);
        public int RpcPort => int.Parse(this.ConfigParameters["rpcport"]);
        public int ApiPort => int.Parse(this.ConfigParameters["apiport"]);

        /// <summary>Location of the data directory for the node.</summary>
        public string DataFolder => this.runner.DataFolder;

        public IPEndPoint Endpoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.ProtocolPort);

        public string Config { get; }

        public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

        public bool CookieAuth { get; set; }

        public CoreNode(NodeRunner runner, NodeBuilder builder, string configfile, bool useCookieAuth = false)
        {
            this.runner = runner;

            this.State = CoreNodeState.Stopped;
            string pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
            this.creds = new NetworkCredential(pass, pass);
            this.Config = Path.Combine(this.runner.DataFolder, configfile);
            this.CookieAuth = useCookieAuth;
            this.ConfigParameters.Import(builder.ConfigParameters);
            var randomFoundPorts = new int[3];
            IpHelper.FindPorts(randomFoundPorts);
            this.ConfigParameters.SetDefaultValueIfUndefined("port", randomFoundPorts[0].ToString());
            this.ConfigParameters.SetDefaultValueIfUndefined("rpcport", randomFoundPorts[1].ToString());
            this.ConfigParameters.SetDefaultValueIfUndefined("apiport", randomFoundPorts[2].ToString());

            CreateConfigFile(this.ConfigParameters);
        }

        /// <summary>Get stratis full node if possible.</summary>
        public FullNode FullNode => this.runner.FullNode;

        public CoreNodeState State { get; private set; }

        private string GetRPCAuth()
        {
            if (!this.CookieAuth)
                return this.creds.UserName + ":" + this.creds.Password;
            else
                return "cookiefile=" + Path.Combine(this.runner.DataFolder, "regtest", ".cookie");
        }

        public void NotInIBD()
        {
            ((InitialBlockDownloadStateMock)this.FullNode.NodeService<IInitialBlockDownloadState>()).SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
        }

        public RPCClient CreateRPCClient()
        {
            return new RPCClient(this.GetRPCAuth(), new Uri("http://127.0.0.1:" + this.RpcPort + "/"), Network.RegTest);
        }

        public INetworkPeer CreateNetworkPeerClient()
        {
            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            var networkPeerFactory = new NetworkPeerFactory(this.runner.Network, DateTimeProvider.Default, loggerFactory, new PayloadProvider().DiscoverPayloads(), new SelfEndpointTracker());
            return networkPeerFactory.CreateConnectedNetworkPeerAsync("127.0.0.1:" + this.ProtocolPort).GetAwaiter().GetResult();
        }

        public void Start()
        {
            lock (this.lockObject)
            {
                this.runner.Start();
                this.State = CoreNodeState.Starting;
            }

            if (this.runner is BitcoinCoreRunner)
                StartBitcoinCoreRunner();
            else
                StartStratisRunner();

            this.State = CoreNodeState.Running;
        }

        private void CreateConfigFile(NodeConfigParameters configParameters = null)
        {
            Directory.CreateDirectory(this.runner.DataFolder);

            configParameters = configParameters ?? new NodeConfigParameters();
            configParameters.SetDefaultValueIfUndefined("regtest", "1");
            configParameters.SetDefaultValueIfUndefined("rest", "1");
            configParameters.SetDefaultValueIfUndefined("server", "1");
            configParameters.SetDefaultValueIfUndefined("txindex", "1");
            if (!this.CookieAuth)
            {
                configParameters.SetDefaultValueIfUndefined("rpcuser", this.creds.UserName);
                configParameters.SetDefaultValueIfUndefined("rpcpassword", this.creds.Password);
            }

            configParameters.SetDefaultValueIfUndefined("printtoconsole", "1");
            configParameters.SetDefaultValueIfUndefined("keypool", "10");
            configParameters.SetDefaultValueIfUndefined("agentprefix", "node" + this.ProtocolPort);
            configParameters.Import(this.ConfigParameters);
            File.WriteAllText(this.Config, configParameters.ToString());
        }

        public void Restart()
        {
            this.Kill();
            this.Start();
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
                await peer.SendMessageAsync(ping, cancellation);

                while ((await listener.ReceivePayloadAsync<PongPayload>(cancellation).ConfigureAwait(false)).Nonce != ping.Nonce)
                {
                }

                DateTimeOffset after = DateTimeOffset.UtcNow;

                return after - before;
            }
        }


        public void SelectMempoolTransactions()
        {
            RPCClient rpc = this.CreateRPCClient();
            uint256[] txs = rpc.GetRawMempool();
            Task<Transaction>[] tasks = txs.Select(t => rpc.GetRawTransactionAsync(t)).ToArray();
            Task.WaitAll(tasks);
            this.transactions.AddRange(tasks.Select(t => t.Result).ToArray());
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

        public void SetDummyMinerSecret(BitcoinSecret secret)
        {
            this.MinerSecret = secret;
        }

        public BitcoinSecret MinerSecret { get; private set; }

        public async Task<Block[]> GenerateAsync(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
        {
            RPCClient rpc = this.CreateRPCClient();
            BitcoinSecret dest = this.GetFirstSecret(rpc);
            uint256 bestBlock = rpc.GetBestBlockHash();
            var blocks = new List<Block>();
            DateTimeOffset now = this.MockTime == null ? DateTimeOffset.UtcNow : this.MockTime.Value;

            using (INetworkPeer peer = this.CreateNetworkPeerClient())
            {
                peer.VersionHandshakeAsync().GetAwaiter().GetResult();

                var chain = bestBlock == this.runner.Network.GenesisHash ? new ConcurrentChain(this.runner.Network) : this.GetChain(peer);

                for (int i = 0; i < blockCount; i++)
                {
                    uint nonce = 0;

                    var block = this.runner.Network.Consensus.ConsensusFactory.CreateBlock();
                    block.Header.HashPrevBlock = chain.Tip.HashBlock;
                    block.Header.Bits = block.Header.GetWorkRequired(rpc.Network, chain.Tip);
                    block.Header.UpdateTime(now, rpc.Network, chain.Tip);

                    var coinbase = this.runner.Network.CreateTransaction();
                    coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
                    coinbase.AddOutput(new TxOut(rpc.Network.GetReward(chain.Height + 1), dest.GetAddress()));
                    block.AddTransaction(coinbase);

                    if (includeUnbroadcasted)
                    {
                        this.transactions = CoreNodeExtensions.Reorder(this.transactions);
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
            var chain = new ConcurrentChain(peer.Network);
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
        private IEnumerable<ChainedHeader> SynchronizeChain(INetworkPeer peer, ChainBase chain, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            ChainedHeader oldTip = chain.Tip;
            List<ChainedHeader> headers = this.GetHeadersFromFork(peer, oldTip, hashStop, cancellationToken).ToList();
            if (headers.Count == 0)
                return new ChainedHeader[0];

            ChainedHeader newTip = headers[headers.Count - 1];

            if (newTip.Height <= oldTip.Height)
                throw new ProtocolException("No tip should have been recieved older than the local one");

            foreach (ChainedHeader header in headers)
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

        public IEnumerable<ChainedHeader> GetHeadersFromFork(INetworkPeer peer, ChainedHeader currentTip, uint256 hashStop = null, CancellationToken cancellationToken = default(CancellationToken))
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
                        BlockLocator = awaited,
                        HashStop = hashStop
                    }, cancellationToken).GetAwaiter().GetResult();

                    while (true)
                    {
                        bool isOurs = false;
                        HeadersPayload headers = null;

                        using (CancellationTokenSource headersCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
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
                                ChainedHeader tempCurrentTip = currentTip;
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
                                currentTip = new ChainedHeader(header, hash, currentTip);

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
            var state = new MempoolValidationState(true);
            return this.runner.FullNode.MempoolManager().Validator.AcceptToMemoryPool(state, trx).Result;
        }

        public List<uint256> GenerateStratisWithMiner(int blockCount)
        {
            return this.FullNode.Services.ServiceProvider.GetService<IPowMining>().GenerateBlocks(new ReserveScript { ReserveFullNodeScript = this.MinerSecret.ScriptPubKey }, (ulong)blockCount, uint.MaxValue);
        }

        public async Task BroadcastBlocksAsync(Block[] blocks, INetworkPeer peer)
        {
            foreach (Block block in blocks)
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

        private BitcoinSecret GetFirstSecret(RPCClient rpc)
        {
            if (this.MinerSecret != null)
                return this.MinerSecret;

            BitcoinSecret dest = rpc.ListSecrets().FirstOrDefault();
            if (dest != null) return dest;

            BitcoinAddress address = rpc.GetNewAddress();
            dest = rpc.DumpPrivKey(address);
            return dest;
        }
    }
}
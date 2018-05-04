using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Peer behavior for memory pool.
    /// Provides message handling of notifications from attached peer.
    /// </summary>
    public class MempoolBehavior : NetworkPeerBehavior
    {
        /// <summary>
        /// Average delay between trickled inventory transmissions in seconds.
        /// Blocks and whitelisted receivers bypass this, outbound peers get half this delay.
        /// </summary>
        private const int InventoryBroadcastInterval = 5;

        /// <summary>
        /// Maximum number of inventory items to send per transmission.
        /// Limits the impact of low-fee transaction floods.
        /// </summary>
        private const int InventoryBroadcastMax = 7 * InventoryBroadcastInterval;

        /// <summary>Memory pool validator for validating transactions.</summary>
        private readonly IMempoolValidator validator;

        /// <summary>Memory pool manager for managing the memory pool.</summary>
        private readonly MempoolManager manager;

        /// <summary>Memory pool orphans for managing orphan transactions.</summary>
        private readonly MempoolOrphans orphans;

        /// <summary>Connection manager for managing peer connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary>Peer notifications available to subscribe to.</summary>
        private readonly Signals.Signals signals;

        /// <summary>Instance logger for the memory pool component.</summary>
        private readonly ILogger logger;

        private readonly Network network;

        /// <summary>
        /// Inventory transaction to send.
        /// State that is local to the behavior.
        /// </summary>
        private readonly Dictionary<uint256, uint256> inventoryTxToSend;

        /// <summary>
        /// Filter for inventory known.
        /// State that is local to the behavior.
        /// </summary>
        private readonly Dictionary<uint256, uint256> filterInventoryKnown;

        /// <summary>
        /// Constructs an instance of memory pool behavior.
        /// </summary>
        /// <param name="validator">Memory pool validator for validating transactions.</param>
        /// <param name="manager">Memory pool manager for managing the memory pool.</param>
        /// <param name="orphans">Memory pool orphans for managing orphan transactions.</param>
        /// <param name="connectionManager">Connection manager for managing peer connections.</param>
        /// <param name="initialBlockDownloadState">Provider of IBD state.</param>
        /// <param name="signals">Peer notifications available to subscribe to.</param>
        /// <param name="logger">Instance logger for memory pool behavior.</param>
        /// <param name="network">The blockchain network.</param>
        public MempoolBehavior(
            IMempoolValidator validator,
            MempoolManager manager,
            MempoolOrphans orphans,
            IConnectionManager connectionManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            Signals.Signals signals,
            ILogger logger,
            Network network)
        {
            this.validator = validator;
            this.manager = manager;
            this.orphans = orphans;
            this.connectionManager = connectionManager;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.signals = signals;
            this.logger = logger;
            this.network = network;

            this.inventoryTxToSend = new Dictionary<uint256, uint256>();
            this.filterInventoryKnown = new Dictionary<uint256, uint256>();
        }

        /// <summary>
        /// Constructs and instance of memory pool behavior.
        /// Constructs a logger instance for memory pool behavior object.
        /// </summary>
        /// <param name="validator">Memory pool validator for validating transactions.</param>
        /// <param name="manager">Memory pool manager for managing the memory pool.</param>
        /// <param name="orphans">Memory pool orphans for managing orphan transactions.</param>
        /// <param name="connectionManager">Connection manager for managing peer connections.</param>
        /// <param name="initialBlockDownloadState">Provider of IBD state.</param>
        /// <param name="signals">Peer notifications available to subscribe to.</param>
        /// <param name="loggerFactory">Logger factory for creating logger.</param>
        /// <param name="network">The blockchain network.</param>
        public MempoolBehavior(
            IMempoolValidator validator,
            MempoolManager manager,
            MempoolOrphans orphans,
            IConnectionManager connectionManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            Signals.Signals signals,
            ILoggerFactory loggerFactory,
            Network network)
            : this(validator, manager, orphans, connectionManager, initialBlockDownloadState, signals, loggerFactory.CreateLogger(typeof(MempoolBehavior).FullName), network)
        {
        }

        /// <summary>Time of last memory pool request in unix time.</summary>
        public long LastMempoolReq { get; private set; }

        /// <summary>Time of next inventory send in unix time.</summary>
        public long NextInvSend { get; set; }

        /// <summary>Whether memory pool is in state where it is ready to send it's inventory.</summary>
        public bool CanSend
        {
            get
            {
                if (!this.AttachedPeer?.PeerVersion?.Relay ?? true)
                    return false;

                // Check whether periodic sends should happen
                bool sendTrickle = this.AttachedPeer.Behavior<ConnectionManagerBehavior>().Whitelisted;

                if (this.NextInvSend < this.manager.DateTimeProvider.GetTime())
                {
                    sendTrickle = true;
                    // Use half the delay for outbound peers, as there is less privacy concern for them.
                    this.NextInvSend = this.manager.DateTimeProvider.GetTime() + 10;
                    // TODO: PoissonNextSend(nNow, InventoryBroadcastInterval >> !pto->fInbound);
                }

                return sendTrickle;
            }
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new MempoolBehavior(this.validator, this.manager, this.orphans, this.connectionManager, this.initialBlockDownloadState, this.signals, this.logger, this.network);
        }

        /// <summary>
        /// Handler for processing incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="message">Incoming message.</param>
        /// <remarks>
        /// TODO: Fix the exception handling of the async event.
        /// </remarks>
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);
            // TODO: Add exception handling
            // TODO: this should probably be on the mempool scheduler and wrapped in a try/catch
            // async void methods are considered fire and forget and not catch exceptions (typical for event handlers)
            // Exceptions will bubble to the execution context, we don't want that!

            try
            {
                await this.ProcessMessageAsync(peer, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                return;
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Handler for processing peer messages.
        /// Handles the following message payloads: TxPayload, MempoolPayload, GetDataPayload, InvPayload.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="message">Incoming message.</param>
        private async Task ProcessMessageAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            try
            {
                switch (message.Message.Payload)
                {
                    case TxPayload txPayload:
                        await this.ProcessTxPayloadAsync(peer, txPayload).ConfigureAwait(false);
                        break;

                    case MempoolPayload mempoolPayload:
                        await this.SendMempoolPayloadAsync(peer, mempoolPayload).ConfigureAwait(false);
                        break;

                    case GetDataPayload getDataPayload:
                        await this.ProcessGetDataAsync(peer, getDataPayload).ConfigureAwait(false);
                        break;

                    case InvPayload invPayload:
                        await this.ProcessInvAsync(peer, invPayload).ConfigureAwait(false);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                return;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Send the memory pool payload to the attached peer.
        /// Gets the transaction info from the memory pool and sends to the attached peer.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="message">The message payload.</param>
        private async Task SendMempoolPayloadAsync(INetworkPeer peer, MempoolPayload message)
        {
            Guard.NotNull(peer, nameof(peer));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Command);
            if (peer != this.AttachedPeer)
            {
                this.logger.LogDebug("Attached peer '{0}' does not match the originating peer '{1}'.", this.AttachedPeer?.RemoteSocketEndpoint, peer.RemoteSocketEndpoint);
                this.logger.LogTrace("(-)[PEER_MISMATCH]");
                return;
            }

            if (!this.CanSend)
                return;

            //if (!(pfrom->GetLocalServices() & NODE_BLOOM) && !pfrom->fWhitelisted)
            //{
            //  LogPrint("net", "mempool request with bloom filters disabled, disconnect peer=%d\n", pfrom->GetId());
            //  pfrom->fDisconnect = true;
            //  return true;
            //}

            //if (connman.OutboundTargetReached(false) && !pfrom->fWhitelisted)
            //{
            //  LogPrint("net", "mempool request with bandwidth limit reached, disconnect peer=%d\n", pfrom->GetId());
            //  pfrom->fDisconnect = true;
            //  return true;
            //}

            List<TxMempoolInfo> vtxinfo = await this.manager.InfoAllAsync();
            Money filterrate = Money.Zero;

            // TODO: implement minFeeFilter
            //{
            //  LOCK(pto->cs_feeFilter);
            //  filterrate = pto->minFeeFilter;
            //}

            List<TxMempoolInfo> sends = await this.manager.MempoolLock.WriteAsync(() =>
            {
                List<TxMempoolInfo> ret = new List<TxMempoolInfo>();
                foreach (TxMempoolInfo txinfo in vtxinfo)
                {
                    uint256 hash = txinfo.Trx.GetHash();
                    this.inventoryTxToSend.Remove(hash);
                    if (filterrate != Money.Zero)
                        if (txinfo.FeeRate.FeePerK < filterrate)
                            continue;
                    this.filterInventoryKnown.TryAdd(hash, hash);
                }
                return ret;
            });

            this.logger.LogTrace("Sending transaction inventory to peer '{0}'.", peer.RemoteSocketEndpoint);
            await this.SendAsTxInventoryAsync(peer, sends.Select(s => s.Trx.GetHash()));
            this.LastMempoolReq = this.manager.DateTimeProvider.GetTime();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processing of inventory payload message from the peer.
        /// Adds inventory to known inventory then sends GetDataPayload to the attached peer.
        /// </summary>
        /// <param name="peer">The peer sending the message.</param>
        /// <param name="invPayload">The inventory payload in the message.</param>
        private async Task ProcessInvAsync(INetworkPeer peer, InvPayload invPayload)
        {
            Guard.NotNull(peer, nameof(peer));
            this.logger.LogTrace("({0}:'{1}',{2}.{3}.{4}:{5})", nameof(peer), peer.RemoteSocketEndpoint, nameof(invPayload), nameof(invPayload.Inventory), nameof(invPayload.Inventory.Count), invPayload.Inventory.Count);
            if (peer != this.AttachedPeer)
            {
                this.logger.LogDebug("Attached peer '{0}' does not match the originating peer '{1}'.", this.AttachedPeer?.RemoteSocketEndpoint, peer.RemoteSocketEndpoint);
                this.logger.LogTrace("(-)[PEER_MISMATCH]");
                return;
            }

            if (invPayload.Inventory.Count > ConnectionManager.MaxInventorySize)
            {
                this.logger.LogTrace("(-)[MAX_INV_SZ]");
                //Misbehaving(pfrom->GetId(), 20); // TODO: Misbehaving
                return; //error("message inv size() = %u", vInv.size());
            }

            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogTrace("(-)[IS_IBD]");
                return;
            }

            bool blocksOnly = !this.manager.mempoolSettings.RelayTxes;
            // Allow whitelisted peers to send data other than blocks in blocks only mode if whitelistrelay is true
            if (peer.Behavior<ConnectionManagerBehavior>().Whitelisted && this.manager.mempoolSettings.WhiteListRelay)
                blocksOnly = false;

            //uint32_t nFetchFlags = GetFetchFlags(pfrom, chainActive.Tip(), chainparams.GetConsensus());

            GetDataPayload send = new GetDataPayload();
            foreach (InventoryVector inv in invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
            {
                //inv.type |= nFetchFlags;

                if (blocksOnly)
                    this.logger.LogInformation("Transaction ID '{0}' inventory sent in violation of protocol peer '{1}'.", inv.Hash, peer.RemoteSocketEndpoint);

                if (await this.orphans.AlreadyHaveAsync(inv.Hash))
                {
                    this.logger.LogDebug("Transaction ID '{0}' already in orphans, skipped.", inv.Hash);
                    continue;
                }

                send.Inventory.Add(inv);
            }

            // add to known inventory
            await this.manager.MempoolLock.WriteAsync(() =>
            {
                foreach (InventoryVector inventoryVector in send.Inventory)
                    this.filterInventoryKnown.TryAdd(inventoryVector.Hash, inventoryVector.Hash);
            });

            if (peer.IsConnected)
            {
                this.logger.LogTrace("Sending transaction inventory to peer '{0}'.", peer.RemoteSocketEndpoint);
                await peer.SendMessageAsync(send).ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processing of the get data payload message from the peer.
        /// Sends the memory pool transaction info via TxPayload to the attached peer.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="getDataPayload">The payload for the message.</param>
        private async Task ProcessGetDataAsync(INetworkPeer peer, GetDataPayload getDataPayload)
        {
            Guard.NotNull(peer, nameof(peer));
            this.logger.LogTrace("({0}:'{1}',{2}.{3}.{4}:{5})", nameof(peer), peer.RemoteSocketEndpoint, nameof(getDataPayload), nameof(getDataPayload.Inventory), nameof(getDataPayload.Inventory.Count), getDataPayload.Inventory.Count);
            if (peer != this.AttachedPeer)
            {
                this.logger.LogDebug("Attached peer '{0}' does not match the originating peer '{1}'.", this.AttachedPeer?.RemoteSocketEndpoint, peer.RemoteSocketEndpoint);
                this.logger.LogTrace("(-)[PEER_MISMATCH]");
                return;
            }

            foreach (InventoryVector item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
            {
                // TODO: check if we need to add support for "not found"

                TxMempoolInfo trxInfo = await this.manager.InfoAsync(item.Hash).ConfigureAwait(false);

                if (trxInfo != null)
                    // TODO: strip block of witness if peer does not support
                    if (peer.IsConnected)
                    {
                        this.logger.LogTrace("Sending transaction '{0}' to peer '{1}'.", item.Hash, peer.RemoteSocketEndpoint);
                        await peer.SendMessageAsync(new TxPayload(trxInfo.Trx.WithOptions(peer.SupportedTransactionOptions, this.network.Consensus.ConsensusFactory))).ConfigureAwait(false);
                    }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processing of the transaction payload message from peer.
        /// Adds transaction from the transaction payload to the memory pool.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="transactionPayload">The payload for the message.</param>
        private async Task ProcessTxPayloadAsync(INetworkPeer peer, TxPayload transactionPayload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(peer), peer.RemoteSocketEndpoint, nameof(transactionPayload), nameof(transactionPayload.Obj), transactionPayload?.Obj?.GetHash());
            Transaction trx = transactionPayload.Obj;
            uint256 trxHash = trx.GetHash();

            // add to local filter
            await this.manager.MempoolLock.WriteAsync(() => this.filterInventoryKnown.TryAdd(trxHash, trxHash));

            MempoolValidationState state = new MempoolValidationState(true);
            if (!await this.orphans.AlreadyHaveAsync(trxHash) && await this.validator.AcceptToMemoryPool(state, trx))
            {
                await this.validator.SanityCheck();
                await this.RelayTransaction(trxHash).ConfigureAwait(false);

                this.signals.SignalTransaction(trx);

                long mmsize = state.MempoolSize;
                long memdyn = state.MempoolDynamicSize;

                this.logger.LogInformation("Transaction ID '{0}' accepted to memory pool from peer '{1}' (poolsz {2} txn, {3} kb).", trxHash, peer.RemoteSocketEndpoint, mmsize, memdyn / 1000);

                await this.orphans.ProcessesOrphansAsync(this, trx);
            }
            else if (state.MissingInputs)
            {
                await this.orphans.ProcessesOrphansMissingInputsAsync(peer, trx).ConfigureAwait(false);
            }
            else
            {
                if (!trx.HasWitness && state.CorruptionPossible)
                {
                }

                // TODO: Implement Processes whitelistforcerelay
            }

            if (state.IsInvalid)
            {
                this.logger.LogInformation("Transaction ID '{0}' from peer '{1}' was not accepted. Invalid state of '{2}'.", trxHash, peer.RemoteSocketEndpoint, state);
            }
            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sends transactions as inventory to attached peer.
        /// </summary>
        /// <param name="peer">Peer to send transactions to.</param>
        /// <param name="trxList">List of transactions.</param>
        private async Task SendAsTxInventoryAsync(INetworkPeer peer, IEnumerable<uint256> trxList)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(peer), peer.RemoteSocketEndpoint, nameof(trxList), "trxList.Count", trxList?.Count());

            Queue<InventoryVector> queue = new Queue<InventoryVector>(trxList.Select(s => new InventoryVector(peer.AddSupportedOptions(InventoryType.MSG_TX), s)));
            while (queue.Count > 0)
            {
                InventoryVector[] items = queue.TakeAndRemove(ConnectionManager.MaxInventorySize).ToArray();
                if (peer.IsConnected)
                {
                    this.logger.LogTrace("Sending transaction inventory to peer '{0}'.", peer.RemoteSocketEndpoint);
                    await peer.SendMessageAsync(new InvPayload(items)).ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Relays a transaction to the connected peers.
        /// </summary>
        /// <param name="hash">Hash of the transaction.</param>
        public Task RelayTransaction(uint256 hash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(hash), hash);
            IReadOnlyNetworkPeerCollection peers = this.connectionManager.ConnectedPeers;
            if (!peers.Any())
            {
                this.logger.LogTrace("(-)[NO_PEERS]");
                return Task.CompletedTask;
            }

            // find all behaviours then start an exclusive task
            // to add the hash to each local collection
            IEnumerable<MempoolBehavior> behaviours = peers.Select(s => s.Behavior<MempoolBehavior>());
            this.logger.LogTrace("(-)");
            return this.manager.MempoolLock.WriteAsync(() =>
            {
                foreach (MempoolBehavior mempoolBehavior in behaviours)
                {
                    if (mempoolBehavior?.AttachedPeer.PeerVersion.Relay ?? false)
                        if (!mempoolBehavior.filterInventoryKnown.ContainsKey(hash))
                            mempoolBehavior.inventoryTxToSend.TryAdd(hash, hash);
                }
            });
        }

        /// <summary>
        /// Sends transaction inventory to attached peer.
        /// This is executed on a 10 second loop when MempoolSignaled is constructed.
        /// </summary>
        public async Task SendTrickleAsync()
        {
            this.logger.LogTrace("()");

            if (!this.CanSend)
            {
                this.logger.LogTrace("(-)[NO_SEND]");
                return;
            }

            INetworkPeer peer = this.AttachedPeer;
            if (peer == null)
            {
                this.logger.LogTrace("(-)[NO_PEER]");
                return;
            }

            // before locking an exclusive task
            // check if there is anything to processes
            if (!await this.manager.MempoolLock.ReadAsync(() => this.inventoryTxToSend.Keys.Any()))
            {
                this.logger.LogTrace("(-)[NO_TXS]");
                return;
            }

            List<uint256> sends = await this.manager.MempoolLock.WriteAsync(() =>
            {
                this.logger.LogTrace("Creating list of transaction inventory to send.");
                
                // Determine transactions to relay
                // Produce a vector with all candidates for sending
                List<uint256> invs = this.inventoryTxToSend.Keys.Take(InventoryBroadcastMax).ToList();
                List<uint256> ret = new List<uint256>();
                foreach (uint256 hash in invs)
                {
                    // Remove it from the to-be-sent set
                    this.inventoryTxToSend.Remove(hash);

                    // Check if not in the filter already
                    if (this.filterInventoryKnown.ContainsKey(hash))
                        continue;
                    
                    // Not in the mempool anymore? don't bother sending it.
                    TxMempoolInfo txInfo = this.manager.Info(hash);
                    if (txInfo == null)
                        continue;
                    
                    //if (filterrate && txinfo.feeRate.GetFeePerK() < filterrate) // TODO:filterrate
                    //{
                    //  continue;
                    //}
                    ret.Add(hash);
                }

                this.logger.LogTrace("Transaction inventory list created.");
                return ret;
            });

            if (sends.Any())
            {
                this.logger.LogTrace("Sending transaction inventory to peer '{0}'.", peer.RemoteSocketEndpoint);
                try
                {
                    await this.SendAsTxInventoryAsync(peer, sends).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                    return;
                }
            }

            this.logger.LogTrace("(-)");
        }
    }
}

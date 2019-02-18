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
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

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
        /// Blocks and white-listed receivers bypass this, outbound peers get half this delay.
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
        private readonly MempoolManager mempoolManager;

        /// <summary>Memory pool orphans for managing orphan transactions.</summary>
        private readonly MempoolOrphans orphans;

        /// <summary>Connection manager for managing peer connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary>Peer notifications available to subscribe to.</summary>
        private readonly Signals.ISignals signals;

        /// <summary>Instance logger for the memory pool behavior component.</summary>
        private readonly ILogger logger;

        /// <summary>Factory used to create the logger for this component.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>The network that this component is running on.</summary>
        private readonly Network network;

        /// <summary>
        /// Inventory transaction to send.
        /// State that is local to the behavior.
        /// </summary>
        private readonly HashSet<uint256> inventoryTxToSend;

        /// <summary>
        /// Filter for inventory known.
        /// State that is local to the behavior.
        /// </summary>
        private readonly HashSet<uint256> filterInventoryKnown;

        /// <summary>
        /// Locking object for memory pool behaviour.
        /// </summary>
        private readonly object lockObject;

        /// <summary>
        /// If the attached peer is whitelisted for relaying.
        /// This is turned on if the peer is whitelisted and whitelistrelay mempool option is also on.
        /// </summary>
        private bool isPeerWhitelistedForRelay;

        /// <summary>
        /// Whether the attached peer should only be relayed blocks (no transactions).
        /// </summary>
        private bool isBlocksOnlyMode;

        public MempoolBehavior(
            IMempoolValidator validator,
            MempoolManager mempoolManager,
            MempoolOrphans orphans,
            IConnectionManager connectionManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            Signals.ISignals signals,
            ILoggerFactory loggerFactory,
            Network network)
        {
            this.validator = validator;
            this.mempoolManager = mempoolManager;
            this.orphans = orphans;
            this.connectionManager = connectionManager;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.signals = signals;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.network = network;

            this.lockObject = new object();
            this.inventoryTxToSend = new HashSet<uint256>();
            this.filterInventoryKnown = new HashSet<uint256>();
            this.isPeerWhitelistedForRelay = false;
            this.isBlocksOnlyMode = false;
        }

        /// <summary>Time of last memory pool request in unix time.</summary>
        public long LastMempoolReq { get; private set; }

        /// <inheritdoc />
        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
            this.isPeerWhitelistedForRelay = this.AttachedPeer.IsWhitelisted() && this.mempoolManager.mempoolSettings.WhiteListRelay;
            this.isBlocksOnlyMode = !this.connectionManager.ConnectionSettings.RelayTxes && !this.isPeerWhitelistedForRelay;
        }

        /// <inheritdoc />
        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        [NoTrace]
        public override object Clone()
        {
            return new MempoolBehavior(this.validator, this.mempoolManager, this.orphans, this.connectionManager, this.initialBlockDownloadState, this.signals, this.loggerFactory, this.network);
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
        }

        /// <summary>
        /// Handler for processing peer messages.
        /// Handles the following message payloads: TxPayload, MempoolPayload, GetDataPayload, InvPayload.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="message">Incoming message.</param>
        private async Task ProcessMessageAsync(INetworkPeer peer, IncomingMessage message)
        {
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
            if (peer != this.AttachedPeer)
            {
                this.logger.LogDebug("Attached peer '{0}' does not match the originating peer '{1}'.", this.AttachedPeer?.RemoteSocketEndpoint, peer.RemoteSocketEndpoint);
                this.logger.LogTrace("(-)[PEER_MISMATCH]");
                return;
            }

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

            List<TxMempoolInfo> transactionsInMempool = await this.mempoolManager.InfoAllAsync().ConfigureAwait(false);
            Money filterrate = Money.Zero;

            // TODO: implement minFeeFilter
            //{
            //  LOCK(pto->cs_feeFilter);
            //  filterrate = pto->minFeeFilter;
            //}

            var transactionsToSend = new List<uint256>();
            lock (this.lockObject)
            {
                foreach (TxMempoolInfo mempoolTransaction in transactionsInMempool)
                {
                    uint256 hash = mempoolTransaction.Trx.GetHash();
                    this.inventoryTxToSend.Remove(hash);
                    if (filterrate != Money.Zero)
                    {
                        if (mempoolTransaction.FeeRate.FeePerK < filterrate)
                        {
                            this.logger.LogTrace("Fee too low, transaction ID '{0}' not added to inventory list.", hash);
                            continue;
                        }
                    }

                    this.filterInventoryKnown.Add(hash);
                    transactionsToSend.Add(hash);
                    this.logger.LogTrace("Added transaction ID '{0}' to inventory list.", hash);
                }
            }

            this.logger.LogTrace("Sending transaction inventory to peer '{0}'.", peer.RemoteSocketEndpoint);
            await this.SendAsTxInventoryAsync(peer, transactionsToSend);
            this.LastMempoolReq = this.mempoolManager.DateTimeProvider.GetTime();
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

            //uint32_t nFetchFlags = GetFetchFlags(pfrom, chainActive.Tip(), chainparams.GetConsensus());

            var inventoryTxs = invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX));

            lock (this.lockObject)
            {
                foreach (var inv in inventoryTxs)
                {
                    this.filterInventoryKnown.Add(inv.Hash);
                }
            }

            var send = new GetDataPayload();
            foreach (var inv in inventoryTxs)
            {
                if (await this.orphans.AlreadyHaveAsync(inv.Hash).ConfigureAwait(false))
                {
                    this.logger.LogDebug("Transaction ID '{0}' already in orphans, skipped.", inv.Hash);
                    continue;
                }

                if (this.isBlocksOnlyMode)
                {
                    this.logger.LogInformation("Transaction ID '{0}' inventory sent in violation of protocol peer '{1}'.", inv.Hash, peer.RemoteSocketEndpoint);
                    continue;
                }

                send.Inventory.Add(new InventoryVector(peer.AddSupportedOptions(InventoryType.MSG_TX), inv.Hash));
            }

            if (peer.IsConnected && (send.Inventory.Count > 0))
            {
                this.logger.LogTrace("Asking for transaction data from peer '{0}'.", peer.RemoteSocketEndpoint);
                await peer.SendMessageAsync(send).ConfigureAwait(false);
            }
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

            foreach (InventoryVector item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
            {
                // TODO: check if we need to add support for "not found"

                TxMempoolInfo trxInfo = await this.mempoolManager.InfoAsync(item.Hash).ConfigureAwait(false);

                if (trxInfo != null)
                {
                    // TODO: strip block of witness if peer does not support
                    if (peer.IsConnected)
                    {
                        this.logger.LogTrace("Sending transaction '{0}' to peer '{1}'.", item.Hash, peer.RemoteSocketEndpoint);
                        await peer.SendMessageAsync(new TxPayload(trxInfo.Trx.WithOptions(peer.SupportedTransactionOptions, this.network.Consensus.ConsensusFactory))).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Processing of the transaction payload message from peer.
        /// Adds transaction from the transaction payload to the memory pool.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="transactionPayload">The payload for the message.</param>
        private async Task ProcessTxPayloadAsync(INetworkPeer peer, TxPayload transactionPayload)
        {
            // Stop processing the transaction early if we are in blocks only mode.
            if (this.isBlocksOnlyMode)
            {
                this.logger.LogDebug("Transaction sent in violation of protocol from peer '{0}'.", peer.RemoteSocketEndpoint);
                this.logger.LogTrace("(-)[BLOCKSONLY]");
                return;
            }

            Transaction trx = transactionPayload.Obj;
            uint256 trxHash = trx.GetHash();

            // add to local filter
            lock (this.lockObject)
            {
                this.filterInventoryKnown.Add(trxHash);
            }
            this.logger.LogTrace("Added transaction ID '{0}' to known inventory filter.", trxHash);

            var state = new MempoolValidationState(true);
            if (!await this.orphans.AlreadyHaveAsync(trxHash) && await this.validator.AcceptToMemoryPool(state, trx))
            {
                await this.validator.SanityCheck();
                this.RelayTransaction(trxHash);

                this.signals.OnTransactionReceived.Notify(trx);

                long mmsize = state.MempoolSize;
                long memdyn = state.MempoolDynamicSize;

                this.logger.LogInformation("Transaction ID '{0}' accepted to memory pool from peer '{1}' (poolsz {2} txn, {3} kb).", trxHash, peer.RemoteSocketEndpoint, mmsize, memdyn / 1000);

                await this.orphans.ProcessesOrphansAsync(this, trx);
            }
            else if (state.MissingInputs)
            {
                this.orphans.ProcessesOrphansMissingInputs(peer, trx);
            }
            else
            {
                // Do not use rejection cache for witness transactions or
                // witness-stripped transactions, as they can have been malleated.
                // See https://github.com/bitcoin/bitcoin/issues/8279 for details.
                if (!trx.HasWitness && !state.CorruptionPossible)
                {
                    this.orphans.AddToRecentRejects(trxHash);
                }

                // Always relay transactions received from whitelisted peers, even
                // if they were already in the mempool or rejected from it due
                // to policy, allowing the node to function as a gateway for
                // nodes hidden behind it.
                //
                // Never relay transactions that we would assign a non-zero DoS
                // score for, as we expect peers to do the same with us in that
                // case.
                if (this.isPeerWhitelistedForRelay)
                {
                    if (!state.IsInvalid)
                    {
                        this.logger.LogDebug("Force relaying transaction ID '{0}' from whitelisted peer '{1}'.", trxHash, peer.RemoteSocketEndpoint);
                        this.RelayTransaction(trxHash);
                    }
                    else
                    {
                        this.logger.LogDebug("Not relaying invalid transaction ID '{0}' from whitelisted peer '{1}' ({2}).", trxHash, peer.RemoteSocketEndpoint, state);
                    }
                }
            }

            if (state.IsInvalid)
            {
                this.logger.LogDebug("Transaction ID '{0}' from peer '{1}' was not accepted. Invalid state of '{2}'.", trxHash, peer.RemoteSocketEndpoint, state);
            }
        }

        /// <summary>
        /// Sends transactions as inventory to attached peer.
        /// </summary>
        /// <param name="peer">Peer to send transactions to.</param>
        /// <param name="trxList">List of transactions.</param>
        private async Task SendAsTxInventoryAsync(INetworkPeer peer, List<uint256> trxList)
        {
            var queue = new Queue<InventoryVector>(trxList.Select(s => new InventoryVector(peer.AddSupportedOptions(InventoryType.MSG_TX), s)));
            while (queue.Count > 0)
            {
                InventoryVector[] items = queue.TakeAndRemove(ConnectionManager.MaxInventorySize).ToArray();
                if (peer.IsConnected)
                {
                    this.logger.LogTrace("Sending transaction inventory to peer '{0}'.", peer.RemoteSocketEndpoint);
                    await peer.SendMessageAsync(new InvPayload(items)).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Adds the transaction to the send inventory for this behavior if it doesn't already exist.
        /// </summary>
        /// <param name="hash">Hash of transaction to add.</param>
        private void AddTransactionToSend(uint256 hash)
        {
            lock (this.lockObject)
            {
                if (!this.filterInventoryKnown.Contains(hash))
                {
                    this.inventoryTxToSend.Add(hash);
                }
            }
        }

        /// <summary>
        /// Relays a transaction to the connected peers.
        /// </summary>
        /// <param name="hash">Hash of the transaction.</param>
        public void RelayTransaction(uint256 hash)
        {
            IReadOnlyNetworkPeerCollection peers = this.connectionManager.ConnectedPeers;
            if (!peers.Any())
            {
                this.logger.LogTrace("(-)[NO_PEERS]");
                return;
            }

            // to add the hash to each local collection
            IEnumerable<MempoolBehavior> behaviours = peers.Select(s => s.Behavior<MempoolBehavior>());
            foreach (MempoolBehavior mempoolBehavior in behaviours)
            {
                var peer = mempoolBehavior?.AttachedPeer;

                if (peer == null)
                {
                    this.logger.LogTrace("Peer is null, skipped.");
                    continue;
                }

                this.logger.LogTrace("Attempting to relaying transaction ID '{0}' to peer '{1}'.", hash, peer.RemoteSocketEndpoint);

                if (peer.PeerVersion.Relay)
                {
                    mempoolBehavior.AddTransactionToSend(hash);
                    this.logger.LogTrace("Added transaction ID '{0}' to send inventory of peer '{1}'.", hash, peer.RemoteSocketEndpoint);
                }
                else
                {
                    this.logger.LogTrace("Peer '{0}' does not support 'Relay', skipped.", peer.RemoteSocketEndpoint);
                }
            }
        }

        /// <summary>
        /// Sends transaction inventory to attached peer.
        /// This is executed on a 5 second loop when MempoolSignaled is constructed.
        /// </summary>
        public async Task SendTrickleAsync()
        {
            INetworkPeer peer = this.AttachedPeer;
            if (peer == null)
            {
                this.logger.LogTrace("(-)[NO_PEER]");
                return;
            }

            var transactionsToSend = new List<uint256>();
            lock (this.lockObject)
            {
                if (!this.inventoryTxToSend.Any())
                {
                    this.logger.LogTrace("(-)[NO_TXS]");
                    return;
                }

                this.logger.LogTrace("Creating list of transaction inventory to send.");

                // Determine transactions to relay
                // Produce a vector with all candidates for sending
                List<uint256> invs = this.inventoryTxToSend.Take(InventoryBroadcastMax).ToList();

                foreach (uint256 hash in invs)
                {
                    // Remove it from the to-be-sent set
                    this.inventoryTxToSend.Remove(hash);
                    this.logger.LogTrace("Transaction ID '{0}' removed from pending sends list.", hash);

                    // Check if not in the filter already
                    if (this.filterInventoryKnown.Contains(hash))
                    {
                        this.logger.LogTrace("Transaction ID '{0}' not added to inventory list, exists in known inventory filter.", hash);
                        continue;
                    }

                    //if (filterrate && txinfo.feeRate.GetFeePerK() < filterrate) // TODO:filterrate
                    //{
                    //  continue;
                    //}
                    transactionsToSend.Add(hash);
                    this.logger.LogTrace("Transaction ID '{0}' added to inventory list.", hash);
                }

                this.logger.LogTrace("Transaction inventory list created.");
            }

            List<uint256> findInMempool = transactionsToSend.ToList();
            foreach (uint256 hash in findInMempool)
            {
                // Not in the mempool anymore? don't bother sending it.
                TxMempoolInfo txInfo = await this.mempoolManager.InfoAsync(hash);
                if (txInfo == null)
                {
                    this.logger.LogTrace("Transaction ID '{0}' not added to inventory list, no longer in mempool.", hash);
                    transactionsToSend.Remove(hash);
                }
            }

            if (transactionsToSend.Any())
            {
                this.logger.LogTrace("Sending transaction inventory to peer '{0}'.", peer.RemoteSocketEndpoint);
                try
                {
                    await this.SendAsTxInventoryAsync(peer, transactionsToSend).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                    return;
                }
            }
        }
    }
}

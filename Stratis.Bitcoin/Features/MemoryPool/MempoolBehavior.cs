using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Node behavior for memory pool.
    /// Provides message handling of notifications from attached node.
    /// </summary>
    public class MempoolBehavior : NodeBehavior
    {
        #region Fields

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

        /// <summary>Connection manager for managing node connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Current block chain state.</summary>
        private readonly ChainState chainState;

        /// <summary>Node notifications available to subscribe to.</summary>
        private readonly Signals.Signals signals;

        /// <summary>Logger for the memory pool component.</summary>
        private readonly ILogger logger;

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

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of memory pool behavior.
        /// </summary>
        /// <param name="validator">Memory pool validator for validating transactions.</param>
        /// <param name="manager">Memory pool manager for managing the memory pool.</param>
        /// <param name="orphans">Memory pool orphans for managing orphan transactions.</param>
        /// <param name="connectionManager">Connection manager for managing node connections.</param>
        /// <param name="chainState">Current block chain state.</param>
        /// <param name="signals">Node notifications available to subscribe to.</param>
        /// <param name="logger">Memory pool behavior logger.</param>
        public MempoolBehavior(
            IMempoolValidator validator,
            MempoolManager manager,
            MempoolOrphans orphans,
            IConnectionManager connectionManager,
            ChainState chainState,
            Signals.Signals signals,
            ILogger logger)
        {
            this.validator = validator;
            this.manager = manager;
            this.orphans = orphans;
            this.connectionManager = connectionManager;
            this.chainState = chainState;
            this.signals = signals;
            this.logger = logger;

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
        /// <param name="connectionManager">Connection manager for managing node connections.</param>
        /// <param name="chainState">Current block chain state.</param>
        /// <param name="signals">Node notifications available to subscribe to.</param>
        /// <param name="loggerFactory">Logger factory for creating logger.</param>
        public MempoolBehavior(
            IMempoolValidator validator,
            MempoolManager manager,
            MempoolOrphans orphans,
            IConnectionManager connectionManager,
            ChainState chainState,
            Signals.Signals signals,
            ILoggerFactory loggerFactory)
            : this(validator, manager, orphans, connectionManager, chainState, signals, loggerFactory.CreateLogger(typeof(MempoolBehavior).FullName))
        {
        }

        #endregion

        #region Properties

        /// <summary>Time of last memory pool request in unix time.</summary>
        public long LastMempoolReq { get; private set; }

        /// <summary>Time of next inventory send in unix time.</summary>
        public long NextInvSend { get; set; }

        /// <summary>Whether memory pool is in state where it is ready to send it's inventory.</summary>
        public bool CanSend
        {
            get
            {
                if (!this.AttachedNode?.PeerVersion?.Relay ?? true)
                    return false;

                // Check whether periodic sends should happen
                bool sendTrickle = this.AttachedNode.Behavior<ConnectionManagerBehavior>().Whitelisted;

                if (this.NextInvSend < this.manager.DateTimeProvider.GetTime())
                {
                    sendTrickle = true;
                    // Use half the delay for outbound peers, as there is less privacy concern for them.
                    this.NextInvSend = this.manager.DateTimeProvider.GetTime() + TimeSpan.TicksPerMinute;
                    // TODO: PoissonNextSend(nNow, InventoryBroadcastInterval >> !pto->fInbound);
                }

                return sendTrickle;
            }
        }

        #endregion

        #region NodeBehavior Overrides

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceived;
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new MempoolBehavior(this.validator, this.manager, this.orphans, this.connectionManager, this.chainState, this.signals, this.logger);
        }

        #endregion

        #region Message Handlers

        /// <summary>
        /// Handler for processing incoming message from node.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="message">Incoming message.</param>
        private async void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            // TODO: Add exception handling
            // TODO: this should probably be on the mempool scheduler and wrapped in a try/catch
            // async void methods are considered fire and forget and not catch exceptions (typical for event handlers)
            // Exceptions will bubble to the execution context, we don't want that!

            try
            {
                await this.AttachedNode_MessageReceivedAsync(node, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException opx)
            {
                if (!opx.CancellationToken.IsCancellationRequested)
                    if (this.AttachedNode?.IsConnected ?? false)
                        throw;

                // do nothing
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.ToString());

                // while in dev catch any unhandled exceptions
                Debugger.Break();
                throw;
            }
        }

        /// <summary>
        /// Handler for processing node messages.
        /// Handles the following message payloads: TxPayload, MempoolPayload, GetDataPayload, InvPayload.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="message">Incoming message.</param>
        private Task AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
        {
            TxPayload txPayload = message.Message.Payload as TxPayload;
            if (txPayload != null)
                return this.ProcessTxPayloadAsync(node, txPayload);

            MempoolPayload mempoolPayload = message.Message.Payload as MempoolPayload;
            if (mempoolPayload != null)
                return this.SendMempoolPayload(node, mempoolPayload);

            GetDataPayload getDataPayload = message.Message.Payload as GetDataPayload;
            if (getDataPayload != null)
                return this.ProcessGetDataAsync(node, getDataPayload);

            InvPayload invPayload = message.Message.Payload as InvPayload;
            if (invPayload != null)
                return this.ProcessInvAsync(node, invPayload);

            return Task.CompletedTask;
        }

        #endregion

        #region Message Processing

        /// <summary>
        /// Send the memory pool payload to the attached node.
        /// Gets the transaction info from the memory pool and sends to the attached node.
        /// </summary>
        /// <param name="node">Node Sending the message.</param>
        /// <param name="message">The message payload.</param>
        private async Task SendMempoolPayload(Node node, MempoolPayload message)
        {
            Guard.Assert(node == this.AttachedNode); // just in case

            if (!this.CanSend)
                return;

            //if (!(pfrom->GetLocalServices() & NODE_BLOOM) && !pfrom->fWhitelisted)
            //{
            //	LogPrint("net", "mempool request with bloom filters disabled, disconnect peer=%d\n", pfrom->GetId());
            //	pfrom->fDisconnect = true;
            //	return true;
            //}

            //if (connman.OutboundTargetReached(false) && !pfrom->fWhitelisted)
            //{
            //	LogPrint("net", "mempool request with bandwidth limit reached, disconnect peer=%d\n", pfrom->GetId());
            //	pfrom->fDisconnect = true;
            //	return true;
            //}

            List<TxMempoolInfo> vtxinfo = await this.manager.InfoAllAsync();
            Money filterrate = Money.Zero;

            // TODO: implement minFeeFilter
            //{
            //	LOCK(pto->cs_feeFilter);
            //	filterrate = pto->minFeeFilter;
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

            await this.SendAsTxInventory(node, sends.Select(s => s.Trx.GetHash()));
            this.LastMempoolReq = this.manager.DateTimeProvider.GetTime();
        }

        /// <summary>
        /// Processing of inventory payload message from the node.
        /// Adds inventory to known inventory then sends GetDataPayload to the attached node.
        /// </summary>
        /// <param name="node">The node sending the message.</param>
        /// <param name="invPayload">The inventory payload in the message.</param>
        private async Task ProcessInvAsync(Node node, InvPayload invPayload)
        {
            Guard.Assert(node == this.AttachedNode); // just in case

            if (invPayload.Inventory.Count > ConnectionManager.MAX_INV_SZ)
            {
                //Misbehaving(pfrom->GetId(), 20); // TODO: Misbehaving
                return; //error("message inv size() = %u", vInv.size());
            }

            if (this.chainState.IsInitialBlockDownload)
                return;

            bool blocksOnly = !this.manager.NodeArgs.Mempool.RelayTxes;
            // Allow whitelisted peers to send data other than blocks in blocks only mode if whitelistrelay is true
            if (node.Behavior<ConnectionManagerBehavior>().Whitelisted && this.manager.NodeArgs.Mempool.WhiteListRelay)
                blocksOnly = false;

            //uint32_t nFetchFlags = GetFetchFlags(pfrom, chainActive.Tip(), chainparams.GetConsensus());

            GetDataPayload send = new GetDataPayload();
            foreach (InventoryVector inv in invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
            {
                //inv.type |= nFetchFlags;

                if (blocksOnly)
                    this.logger.LogInformation($"transaction ({inv.Hash}) inv sent in violation of protocol peer={node.RemoteSocketEndpoint}");

                if (await this.orphans.AlreadyHave(inv.Hash))
                    continue;

                send.Inventory.Add(inv);
            }

            // add to known inventory
            await this.manager.MempoolLock.WriteAsync(() =>
            {
                foreach (InventoryVector inventoryVector in send.Inventory)
                    this.filterInventoryKnown.TryAdd(inventoryVector.Hash, inventoryVector.Hash);
            });

            if (node.IsConnected)
                await node.SendMessageAsync(send).ConfigureAwait(false);
        }

        /// <summary>
        /// Processing of the get data payload message from node.
        /// Sends the memory pool transaction info via TxPayload to the attached node.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="getDataPayload">The payload for the message.</param>
        private async Task ProcessGetDataAsync(Node node, GetDataPayload getDataPayload)
        {
            Guard.Assert(node == this.AttachedNode); // just in case

            foreach (InventoryVector item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
            {
                // TODO: check if we need to add support for "not found" 

                TxMempoolInfo trxInfo = await this.manager.InfoAsync(item.Hash).ConfigureAwait(false);

                if (trxInfo != null)
                    //TODO strip block of witness if node does not support
                    if (node.IsConnected)
                        await node.SendMessageAsync(new TxPayload(trxInfo.Trx.WithOptions(node.SupportedTransactionOptions))).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Processing of the transaction payload message from node.
        /// Adds transaction from the transaction payload to the memory pool.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="transactionPayload">The payload for the message.</param>
        private async Task ProcessTxPayloadAsync(Node node, TxPayload transactionPayload)
        {
            Transaction trx = transactionPayload.Object;
            uint256 trxHash = trx.GetHash();

            // add to local filter
            await this.manager.MempoolLock.WriteAsync(() => this.filterInventoryKnown.TryAdd(trxHash, trxHash));

            MempoolValidationState state = new MempoolValidationState(true);
            if (!await this.orphans.AlreadyHave(trxHash) && await this.validator.AcceptToMemoryPool(state, trx))
            {
                await this.validator.SanityCheck();
                await this.RelayTransaction(trxHash).ConfigureAwait(false);

                this.signals.SignalTransaction(trx);

                long mmsize = state.MempoolSize;
                long memdyn = state.MempoolDynamicSize;

                this.logger.LogInformation($"AcceptToMemoryPool: peer={node.Peer.Endpoint}: accepted {trxHash} (poolsz {mmsize} txn, {memdyn / 1000} kb)");

                await this.orphans.ProcessesOrphans(this, trx);
            }
            else if (state.MissingInputs)
            {
                await this.orphans.ProcessesOrphansMissingInputs(node, trx);
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
                this.logger.LogInformation($"{trxHash} from peer={node.Peer.Endpoint} was not accepted: {state}");
            }
        }

        /// <summary>
        /// Sends transactions as inventory to attached node.
        /// </summary>
        /// <param name="node">Node to receive message.</param>
        /// <param name="trxList">List of transactions.</param>
        private async Task SendAsTxInventory(Node node, IEnumerable<uint256> trxList)
        {
            Queue<InventoryVector> queue = new Queue<InventoryVector>(trxList.Select(s => new InventoryVector(InventoryType.MSG_TX, s)));
            while (queue.Count > 0)
            {
                InventoryVector[] items = queue.TakeAndRemove(ConnectionManager.MAX_INV_SZ).ToArray();
                if (node.IsConnected)
                    await node.SendMessageAsync(new InvPayload(items));
            }
        }

        #endregion

        #region Operations

        /// <summary>
        /// Relays a transaction to the connected nodes.
        /// </summary>
        /// <param name="hash">Hash of the transaction.</param>
        public Task RelayTransaction(uint256 hash)
        {
            IReadOnlyNodesCollection nodes = this.connectionManager.ConnectedNodes;
            if (!nodes.Any())
                return Task.CompletedTask;

            // find all behaviours then start an exclusive task 
            // to add the hash to each local collection
            IEnumerable<MempoolBehavior> behaviours = nodes.Select(s => s.Behavior<MempoolBehavior>());
            return this.manager.MempoolLock.WriteAsync(() =>
            {
                foreach (MempoolBehavior mempoolBehavior in behaviours)
                {
                    if (mempoolBehavior?.AttachedNode.PeerVersion.Relay ?? false)
                        if (!mempoolBehavior.filterInventoryKnown.ContainsKey(hash))
                            mempoolBehavior.inventoryTxToSend.TryAdd(hash, hash);
                }
            });
        }

        /// <summary>
        /// Sends transaction inventory to attached node.
        /// This is executed on a 10 second loop when MempoolSignaled is constructed.
        /// </summary>
        public async Task SendTrickle()
        {
            if (!this.CanSend)
                return;

            // before locking an exclusive task 
            // check if there is anything to processes
            if (!await this.manager.MempoolLock.ReadAsync(() => this.inventoryTxToSend.Keys.Any()))
                return;

            List<uint256> sends = await this.manager.MempoolLock.WriteAsync(() =>
            {
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
                    //	continue;
                    //}
                    ret.Add(hash);
                }
                return ret;
            });

            if (sends.Any())
                await this.SendAsTxInventory(this.AttachedNode, sends);
        }

        #endregion
    }
}

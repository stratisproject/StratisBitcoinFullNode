using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Recipient = Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using TransactionBuildContext = Stratis.FederatedPeg.Features.FederationGateway.Wallet.TransactionBuildContext;

namespace Stratis.FederatedPeg.Features.FederationGateway.CounterChain
{
    ///<inheritdoc/>
    internal class CounterChainSessionManager : ICounterChainSessionManager
    {
        // The wallet that contains the multisig capabilities.
        private readonly IFederationWalletManager federationWalletManager;

        // Transaction handler used to build the final transaction.
        private readonly IFederationWalletTransactionHandler federationWalletTransactionHandler;

        // Peer connector for broadcasting the payloads.
        private readonly IConnectionManager connectionManager;

        // The network we are running.
        private readonly Network network;

        // Gateway settings picked up from the node config.
        private readonly FederationGatewaySettings federationGatewaySettings;

        // Broadcaster we use to pass our payload to peers.
        private readonly IBroadcasterManager broadcastManager;

        // The IBD status.
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        // This is used only in the log file to tell us the block height.
        private readonly ConcurrentChain concurrentChain;

        // The logger. It's the logger.
        private readonly ILogger logger;
        
        // The shoulders we stand on.
        private readonly IFullNode fullnode;

        // The sessions are stored here.
        private readonly ConcurrentDictionary<uint256, CounterChainSession> sessions = new ConcurrentDictionary<uint256, CounterChainSession>();

        private readonly IPAddressComparer ipAddressComparer;

        // Get everything together before we get going.
        public CounterChainSessionManager(
            ILoggerFactory loggerFactory, 
            IFederationWalletManager federationWalletManager,
            IFederationWalletTransactionHandler federationWalletTransactionHandler, 
            IConnectionManager connectionManager, 
            Network network,
            FederationGatewaySettings federationGatewaySettings, 
            IInitialBlockDownloadState initialBlockDownloadState, 
            IFullNode fullnode,
            IBroadcasterManager broadcastManager, 
            ConcurrentChain concurrentChain,
            DataFolder dataFolder,
            IDateTimeProvider dateTimeProvider,
            ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(dataFolder, nameof(dataFolder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.connectionManager = connectionManager;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.concurrentChain = concurrentChain;
            this.fullnode = fullnode;
            this.broadcastManager = broadcastManager;
            this.federationWalletManager = federationWalletManager;
            this.federationWalletTransactionHandler = federationWalletTransactionHandler;
            this.federationGatewaySettings = federationGatewaySettings;
            this.ipAddressComparer = new IPAddressComparer();
        }

        ///<inheritdoc/>
        public void CreateSessionOnCounterChain(uint256 sessionId, Money amount, string destinationAddress)
        {
            // TODO: This is inadequate.
            // We don't process sessions if our chain is not past IBD.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"RunSessionsAsync() CounterChain is in IBD exiting. Height:{this.concurrentChain.Height}.");
                return;
            }
            this.RegisterSession(sessionId, amount, destinationAddress);
        }

        // Add the session to its collection.
        private CounterChainSession RegisterSession(uint256 transactionId, Money amount, string destination)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(transactionId), transactionId, nameof(amount), amount, nameof(destination), destination);

            var counterChainSession = new CounterChainSession(
                this.logger, 
                this.federationGatewaySettings, 
                transactionId, 
                amount,
                destination);
            this.sessions.AddOrReplace(transactionId, counterChainSession);
            return counterChainSession;
        }

        ///<inheritdoc/>
        public void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard)
        {
            this.logger.LogTrace("()");
            this.logger.LogInformation("Receive Partial on {0} for BossCard - {1}", this.network.ToChain(), bossCard);

            string bc = bossCard.ToString();
            var counterChainSession = sessions[sessionId];
            bool hasQuorum = counterChainSession.AddPartial(partialTransaction, bc);

            if (hasQuorum)
            {
                this.logger.LogInformation("Receive Partial: Reached Quorum.");
                BroadcastTransaction(counterChainSession);
            }
            this.logger.LogTrace("(-)");
        }

        // If we have reached the quorum we can combine and broadcast the transaction. 
        private void BroadcastTransaction(CounterChainSession counterChainSession)
        {
            //TODO: can remove this?
            if (this.fullnode.State == FullNodeState.Disposed || this.fullnode.State == FullNodeState.Disposing)
            {
                this.logger.LogInformation("Full node disposing during broadcast. Do nothing.");
                return;
            }

            this.logger.LogTrace("()");
            this.logger.LogInformation("Combine partials and broadcast transaction.");

            counterChainSession.PartialTransactions.ToList()
                .ForEach(t => this.logger.LogInformation(t.ToString()));

            var partials = 
                from t in counterChainSession.PartialTransactions
                where t != null select t;

            var combinedTransaction = this.federationWalletManager.GetWallet().CombinePartialTransactions(partials.ToArray());
            this.logger.LogInformation("Combined transaction: {0}", combinedTransaction);
            this.broadcastManager.BroadcastTransactionAsync(combinedTransaction).GetAwaiter().GetResult();
            this.logger.LogTrace("(-)");
        }

        ///<inheritdoc/>
        public async Task<uint256> ProcessCounterChainSession(uint256 sessionId, Money amount, string destinationAddress)
        {
            //todo this method is doing too much. factor some of this into private methods after we added the counterchainid.
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(sessionId), sessionId, nameof(amount), amount, nameof(destinationAddress), destinationAddress);
            this.logger.LogInformation("Session Registered.");

            //// Check if this has already been done then we just return the transactionId
            //if (this.sessions.TryGetValue(sessionId, out var counterchainSession))
            //{
            //    // This is the mechanism that tells the round robin not to continue and also
            //    // notifies the monitorChain of the completed transactionId from the counterChain transaction.
            //    if (counterchainSession.CounterChainTransactionId != uint256.Zero)
            //    {
            //        // If we get here:
            //        // 1. One of the nodes became the boss and successfully broadcast a completed transaction.
            //        // 2. The monitor in this node received the block with the transaction (identified by the sessionId in the op_return).
            //        // 3. The monitor wrote the CounterChainTransactionId into the counterChainSession to indicate all was done.
            //        // This method then does not try to process the transaction and instead signals to the monitorChain that this
            //        // transaction already completed by passing back the transactionId.
            //        this.logger.LogInformation($"Counterchain Session: {sessionId} was already completed. Doing nothing.");
            //        return counterchainSession.CounterChainTransactionId;
            //    }
            //}

            // Check if the password has been added. If not, no need to go further.
            if (this.federationWalletManager.Secret == null || string.IsNullOrEmpty(this.federationWalletManager.Secret.WalletPassword))
            {
                string errorMessage = "The password needed for signing multisig transactions is missing.";
                this.logger.LogError(errorMessage);
                throw new WalletException(errorMessage);
            }

            //create the partial transaction template
            var wallet = this.federationWalletManager.GetWallet();
            var multiSigAddress = wallet.MultiSigAddress;

            var destination = BitcoinAddress.Create(destinationAddress, this.network).ScriptPubKey;

            // Encode the sessionId into a string.
            this.logger.LogInformation("SessionId encoded bytes length = {0}.", sessionId.ToBytes().Length);

            // We are the Boss so first I build the multisig transaction template.
            // TODO: The password is currently hardcoded here
            var multiSigContext = new TransactionBuildContext(
                (new[] { new Recipient.Recipient { Amount = amount, ScriptPubKey = destination } }).ToList(),
                this.federationWalletManager.Secret.WalletPassword, sessionId.ToBytes())
            {
                TransactionFee = Money.Coins(0.01m),
                MinConfirmations = 1,
                Shuffle = true,
                MultiSig = multiSigAddress,
                IgnoreVerify = true,
                Sign = false
            };

            this.logger.LogInformation("Building template Transaction.");
            var templateTransaction = this.federationWalletTransactionHandler.BuildTransaction(multiSigContext);

            //add my own partial
            this.logger.LogInformation("Signing own partial.");
            var counterChainSession = this.VerifySession(sessionId, templateTransaction);

            if (counterChainSession == null)
            {
                this.logger.LogInformation("CounterChainSession is null, returning.");
                return uint256.One;
            }
            this.MarkSessionAsSigned(counterChainSession);
            var partialTransaction = wallet.SignPartialTransaction(templateTransaction, this.federationWalletManager.Secret.WalletPassword);

            uint256 bossCard = BossTable.MakeBossTableEntry(sessionId, this.federationGatewaySettings.PublicKey);
            this.logger.LogInformation("My bossCard: {0}.", bossCard);
            this.ReceivePartial(sessionId, partialTransaction, bossCard);

            //now build the requests for the partials
            var requestPartialTransactionPayload = new RequestPartialTransactionPayload(sessionId, templateTransaction);

            // Only broadcast to the federation members.
            var federationNetworkPeers =
                this.connectionManager.ConnectedPeers
                .Where(p => !p.Inbound && federationGatewaySettings.FederationNodeIpEndPoints.Any(e => this.ipAddressComparer.Equals(e.Address, p.PeerEndPoint.Address)));
            foreach (INetworkPeer peer in federationNetworkPeers)
            {
                try
                {
                    this.logger.LogInformation("Broadcasting Partial Transaction Request to {0}.", peer.PeerEndPoint);
                    await peer.SendMessageAsync(requestPartialTransactionPayload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            this.logger.LogTrace("(-)");
            return uint256.One;
        }

        ///<inheritdoc/>
        public CounterChainSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate)
        {
            //TODO: This has a critical flaw in the transaction checking. It's not enough to find one ok output. There could be additional rouge outputs.
            //TODO: What are other ways this code can be circumvented?

            var exists = this.sessions.TryGetValue(sessionId, out var counterChainSession);

            this.logger.LogTrace("()");
            this.logger.LogInformation("CounterChainSession exists: {0} sessionId: {1}", exists, sessionId);

            // We do not have this session.
            if (!exists) return null;

            // Have I already signed this session?
            this.logger.LogInformation("HaveISigned:{0}", counterChainSession.HaveISigned);
            if (counterChainSession.HaveISigned)
            {
                this.logger.LogInformation("Fatal: the session {0} has already signed a partial transaction.", sessionId);
                return null;
            }

            // We compare our session values with the values we read from the transaction.
            var scriptPubKeyFromSession = BitcoinAddress.Create(counterChainSession.Destination, this.network).ScriptPubKey;
            var amountFromSession = counterChainSession.Amount;

            bool addressMatches = false;
            bool amountMatches = false;
            foreach (var output in partialTransactionTemplate.Outputs)
            {
                if (output.ScriptPubKey == scriptPubKeyFromSession)
                {
                    addressMatches = true;
                    this.logger.LogInformation("Session {0} found the matching destination address.", sessionId);
                    if (output.Value == amountFromSession)
                    {
                        amountMatches = true;
                        this.logger.LogInformation("Session {0} found the matching amount.", sessionId);
                    }
                }
            }

            // The addess does not match. exit.
            if (!addressMatches)
            {
                this.logger.LogInformation("Fatal: The destination address did not match in session {0}.", sessionId);
                return null;
            }

            // The amount does not match. exit.
            if (!amountMatches)
            {
                this.logger.LogInformation("Fatal: The amount did not match in session {0}.", sessionId);
                return null;
            }

            return counterChainSession;
        }

        ///<inheritdoc/>
        public void MarkSessionAsSigned(CounterChainSession session)
        {
            //TODO: this should be locked. the sessions are 30 seconds apart but network conditions could cause a collision.
            this.logger.LogInformation("has signed session {0}.", session.SessionId);
            session.HaveISigned = true;
        }

        public void AddCounterChainTransactionId(uint256 sessionId, uint256 transactionId)
        {
            if (!this.sessions.TryGetValue(sessionId, out var counterChainSession))
            {
                this.logger.LogInformation($"Session::AddCounterChainTransactionId: The session does not exist. Doing nothing.");
                return;
            }

            if (counterChainSession.CounterChainTransactionId != uint256.Zero)
            {
                this.logger.LogInformation($"Session::AddCounterChainTransactionId: Attempting to write transactionId {transactionId} while session already has transactionId {counterChainSession.CounterChainTransactionId}. Fatal!");
                return;
            }

            counterChainSession.CounterChainTransactionId = transactionId;
            this.logger.LogInformation($"Session::AddCounterChainTransactionId: Session {sessionId} was completed with transactionId {transactionId}.");
        }
    }
}

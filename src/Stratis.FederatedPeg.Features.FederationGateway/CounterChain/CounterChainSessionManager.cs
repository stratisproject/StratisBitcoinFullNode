using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBreeze.Utils;
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
using Stratis.FederatedPeg.Features.FederationGateway.Models;
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

        private readonly Network network;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        // Broadcaster we use to pass our payload to peers.
        private readonly IBroadcasterManager broadcastManager;

        // The IBD status.
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        // This is used only in the log file to tell us the block height.
        private readonly ConcurrentChain concurrentChain;

        private readonly ILogger logger;
        
        private readonly IFullNode fullnode;

        private readonly ConcurrentDictionary<int, CounterChainSession> sessions = new ConcurrentDictionary<int, CounterChainSession>();

        private readonly IPAddressComparer ipAddressComparer;

        public CounterChainSessionManager(
            ILoggerFactory loggerFactory, 
            IFederationWalletManager federationWalletManager,
            IFederationWalletTransactionHandler federationWalletTransactionHandler, 
            IConnectionManager connectionManager, 
            Network network,
            IFederationGatewaySettings federationGatewaySettings, 
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

        // Add the session to its collection.
        private void RegisterSession(int blockHeight, List<CounterChainTransactionInfoRequest> counterChainTransactionInfos)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(blockHeight), blockHeight, "Transactions Count", counterChainTransactionInfos.Count);


            var counterChainSession = new CounterChainSession(this.logger, this.federationGatewaySettings, blockHeight);
            counterChainSession.CrossChainTransactions = counterChainTransactionInfos.Select(c => new CrossChainTransactionInfo
            {
                BlockNumber = blockHeight,
                TransactionHash = c.TransactionHash,
                DestinationAddress = c.DestinationAddress,
                Amount = c.Amount
            }).ToList();

            this.sessions.AddOrReplace(blockHeight, counterChainSession);
        }

        ///<inheritdoc/>
        public void ReceivePartial(int blockHeight, Transaction partialTransaction, uint256 bossCard)
        {
            this.logger.LogTrace("()");
            this.logger.LogInformation("Receive Partial on {0} for BossCard - {1}. Block height: {2}", this.network.ToChain(), bossCard, blockHeight);

            string bc = bossCard.ToString();
            var counterChainSession = sessions[blockHeight];
            bool hasQuorum = counterChainSession.AddPartial(partialTransaction, bc);

            if (hasQuorum)
            {
                this.logger.LogInformation("Receive Partial: Reached Quorum.");
                BroadcastTransaction(counterChainSession);
            }
            this.logger.LogTrace("(-)");
        }

        public void CreateSessionOnCounterChain(int blockHeight, List<CounterChainTransactionInfoRequest> counterChainTransactionInfos)
        {
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"RunSessionsAsync() CounterChain is in IBD exiting. Height:{this.concurrentChain.Height}.");
                return;
            }
            this.RegisterSession(blockHeight, counterChainTransactionInfos);
        }

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
        public async Task<uint256> ProcessCounterChainSession(int blockHeight)
        {
            //todo this method is doing too much. factor some of this into private methods after we added the counterchainid.
            this.logger.LogTrace("({0}:'{1}'", nameof(blockHeight), blockHeight);
            this.logger.LogInformation("Session Registered.");

            // Check if this has already been done then we just return the transactionId
            if (this.sessions.TryGetValue(blockHeight, out var counterchainSession))
            {
                // This is the mechanism that tells the round robin not to continue and also
                // notifies the monitorChain of the completed transactionId from the counterChain transaction.
                if (counterchainSession.CounterChainTransactionId != uint256.Zero)
                {
                    // If we get here:
                    // 1. One of the nodes became the boss and successfully broadcast a completed transaction.
                    // 2. The monitor in this node received the block with the transaction (identified by the sessionId in the op_return).
                    // 3. The monitor wrote the CounterChainTransactionId into the counterChainSession to indicate all was done.
                    // This method then does not try to process the transaction and instead signals to the monitorChain that this
                    // transaction already completed by passing back the transactionId.
                    this.logger.LogInformation($"Counterchain Session for block: {blockHeight} was already completed. Doing nothing.");
                    return counterchainSession.CounterChainTransactionId;
                }
            }
            else
            {
                throw new InvalidOperationException($"No CounterChainSession found in the counter chain for block height {blockHeight}.");
            }

            // Check if the password has been added. If not, no need to go further.
            if (this.federationWalletManager.Secret == null || string.IsNullOrEmpty(this.federationWalletManager.Secret.WalletPassword))
            {
                string errorMessage = "The password needed for signing multisig transactions is missing.";
                this.logger.LogError(errorMessage);
                throw new WalletException(errorMessage);
            }

            var wallet = this.federationWalletManager.GetWallet();
            var multiSigAddress = wallet.MultiSigAddress;

            var recipients = counterchainSession.CrossChainTransactions.Select(s =>
                new Recipient.Recipient
                {
                    Amount = s.Amount,
                    ScriptPubKey = BitcoinAddress.Create(s.DestinationAddress, this.network).ScriptPubKey
                }).ToList();

            // We are the Boss so first I build the multisig transaction template.
            var multiSigContext = new TransactionBuildContext(recipients, this.federationWalletManager.Secret.WalletPassword, Encoding.UTF8.GetBytes(blockHeight.ToString()))
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
            this.logger.LogInformation("Verify own partial.");
            var counterChainSession = this.VerifySession(blockHeight, templateTransaction);

            if (counterChainSession == null)
            {
                var exists = this.sessions.TryGetValue(blockHeight, out counterChainSession);
                if (exists) return counterChainSession.CounterChainTransactionId;
                throw new InvalidOperationException($"No CounterChainSession found in the counter chain for block height {blockHeight}.");
            }
            this.MarkSessionAsSigned(counterChainSession);
            var partialTransaction = wallet.SignPartialTransaction(templateTransaction, this.federationWalletManager.Secret.WalletPassword);

            uint256 bossCard = BossTable.MakeBossTableEntry(blockHeight, this.federationGatewaySettings.PublicKey);
            this.logger.LogInformation("My bossCard: {0}.", bossCard);
            this.ReceivePartial(blockHeight, partialTransaction, bossCard);

            //now build the requests for the partials
            var requestPartialTransactionPayload = new RequestPartialTransactionPayload(templateTransaction, blockHeight);

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
            // We don't want to say this is complete yet.  We wait until we get the transaction back in a block.
            return uint256.Zero;
        }

        ///<inheritdoc/>
        public CounterChainSession VerifySession(int blockHeight, Transaction partialTransactionTemplate)
        {
            //TODO: This has a critical flaw in the transaction checking. It's not enough to find one ok output. There could be additional rouge outputs.
            //TODO: What are other ways this code can be circumvented?

            var exists = this.sessions.TryGetValue(blockHeight, out var counterChainSession);

            this.logger.LogTrace("()");
            this.logger.LogInformation("CounterChainSession exists: {0} sessionId: {1}", exists, blockHeight);

            if (!exists) return null;
            
            this.logger.LogInformation("HaveISigned:{0}", counterChainSession.HaveISigned);
            if (counterChainSession.HaveISigned)
            {
                this.logger.LogInformation("The partial transaction for block {0} has already been signed.", blockHeight);
                return null;
            }

            // We compare our session values with the values we read from the transaction.
            var allAddressesInSession = counterChainSession.CrossChainTransactions.Select(
                trxInfo => new
                {
                    Address = BitcoinAddress.Create(trxInfo.DestinationAddress, this.network).ScriptPubKey,
                    Amount = trxInfo.Amount
                }).ToList();
            var amountByAddress = allAddressesInSession.GroupBy(a => a.Address)
                .Select(a => new {
                    Address = a.Key,
                    TotalAmount = a.Sum(x => x.Amount)
                });

            var outputAddresses = partialTransactionTemplate.Outputs.Select(o => o.ScriptPubKey).Distinct().ToList();
            var allAddressesMatch = (outputAddresses.Count == amountByAddress.Count() + 2)
                                    && amountByAddress.All(a => outputAddresses.Contains(a.Address));
            if (!allAddressesMatch)
            {
                this.logger.LogInformation("Session for block {0} found did not have matching addresses.", blockHeight);
                this.logger.LogInformation("Expected addresses {0}.", string.Join(",", outputAddresses));
                this.logger.LogInformation("Found addresses {0}.", string.Join(",", amountByAddress.Select(a => a.Address)));
                return null;
            }

            foreach (var amount in amountByAddress)
            {
                var match = partialTransactionTemplate.Outputs
                                .Where(o => o.ScriptPubKey == amount.Address)
                                .Sum(o => o.Value) == amount.TotalAmount;
                if (!match)
                {
                    logger.LogInformation("Session for block {0} found mismatch in amounts.", blockHeight);
                    return null;
                }
            }
            return counterChainSession;
        }

        ///<inheritdoc/>
        public void MarkSessionAsSigned(CounterChainSession session)
        {
            //TODO: this should be locked. the sessions are 30 seconds apart but network conditions could cause a collision.
            this.logger.LogInformation("has signed session for block {0}.", session.BlockHeight);
            session.HaveISigned = true;
        }

        public void AddCounterChainTransactionId(int blockHeight, uint256 transactionId)
        {
            if (!this.sessions.TryGetValue(blockHeight, out var counterChainSession))
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
            this.logger.LogInformation($"Session::AddCounterChainTransactionId: Session for block {blockHeight} was completed with transactionId {transactionId}.");
        }
    }
}

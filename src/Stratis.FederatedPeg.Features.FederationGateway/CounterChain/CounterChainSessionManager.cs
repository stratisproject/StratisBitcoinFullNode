using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.FederatedPeg.Features.FederationGateway.CounterChain
{
    ///<inheritdoc/>
    internal class CounterChainSessionManager : ICounterChainSessionManager
    {
        // The wallet that contains the multisig capabilities.
        private IGeneralPurposeWalletManager generalPurposeWalletManager;

        // Transaction handler used to build the final transaction.
        private IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler;

        // Peer connector for broadcasting the payloads.
        private IConnectionManager connectionManager;

        // The network we are running.
        private Network network;

        // Gateway settings picked up from the node config.
        private FederationGatewaySettings federationGatewaySettings;

        // Broadcaster we use to pass our payload to peers.
        private IGeneralPurposeWalletBroadcasterManager broadcastManager;

        // The IBD status.
        private IInitialBlockDownloadState initialBlockDownloadState;

        // This is used only in the log file to tell us the block height.
        private ConcurrentChain concurrentChain;

        // The logger. It's the logger.
        private readonly ILogger logger;

        // The shoulders we stand on.
        private IFullNode fullnode;

        // The sessions are stored here.
        private ConcurrentDictionary<uint256, CounterChainSession> sessions = new ConcurrentDictionary<uint256, CounterChainSession>();

        // Get everything together before we get going.
        public CounterChainSessionManager(ILoggerFactory loggerFactory, IGeneralPurposeWalletManager generalPurposeWalletManager,
            IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler, IConnectionManager connectionManager, Network network,
            FederationGatewaySettings federationGatewaySettings, IInitialBlockDownloadState initialBlockDownloadState, IFullNode fullnode,
            IGeneralPurposeWalletBroadcasterManager broadcastManager, ConcurrentChain concurrentChain, ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.connectionManager = connectionManager;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.concurrentChain = concurrentChain;
            this.fullnode = fullnode;
            this.broadcastManager = broadcastManager;
            this.generalPurposeWalletManager = generalPurposeWalletManager;
            this.generalPurposeWalletTransactionHandler = generalPurposeWalletTransactionHandler;
            this.federationGatewaySettings = federationGatewaySettings;
        }

        ///<inheritdoc/>
        public void CreateSessionOnCounterChain(uint256 sessionId, Money amount, string destinationAddress)
        {
            // TODO: This is inadequate.
            // We don't process sessions if our chain is not past IBD.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() CounterChain is in IBD exiting. Height:{this.concurrentChain.Height}.");
                return;
            }
            this.RegisterSession(sessionId, amount, destinationAddress);
        }

        // Add the session to its collection.
        private CounterChainSession RegisterSession(uint256 transactionId, Money amount, string destination)
        {
            var memberFolderManager = new MemberFolderManager(this.federationGatewaySettings.FederationFolder);
            var federation = memberFolderManager.LoadFederation(this.federationGatewaySettings.MultiSigM, this.federationGatewaySettings.MultiSigN);

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession transactionId:{transactionId}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession amount:{amount}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession destination:{destination}");

            var counterChainSession = new CounterChainSession(this.logger, this.federationGatewaySettings.MultiSigN, transactionId, federation.GetPublicKeys(this.network.ToChain()), amount, destination);
            this.sessions.AddOrReplace(transactionId, counterChainSession);
            return counterChainSession;
        }

        ///<inheritdoc/>
        public void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: {this.network.ToChain()}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: BossCard - {bossCard}");

            string bc = bossCard.ToString();
            var counterChainSession = sessions[sessionId];
            bool hasQuorum = counterChainSession.AddPartial(this.federationGatewaySettings.MemberName, partialTransaction, bc);

            if (hasQuorum)
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: Reached Quorum.");
                BroadcastTransaction(counterChainSession);
            }
            this.logger.LogInformation("(-)");
        }

        // If we have reached the quorum we can combine and broadcast the transaction. 
        private void BroadcastTransaction(CounterChainSession counterChainSession)
        {
            //TODO: we can remove this.
            if (this.fullnode.State == FullNodeState.Disposed || this.fullnode.State == FullNodeState.Disposing)
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Full node disposing during broadcast. Do nothing.");
                return;
            }

            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Combining and Broadcasting transaction.");

            var account = this.generalPurposeWalletManager.GetAccounts(this.federationGatewaySettings.MultiSigWalletName).First();
            if (account == null)
            {
                this.logger.LogInformation("InvalidAccount from GPWallet.");
                return;
            }

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Combine Partials");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} {counterChainSession}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} {counterChainSession.PartialTransactions[0]}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} {counterChainSession.PartialTransactions[1]}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} {counterChainSession.PartialTransactions[2]}");

            var partials = from t in counterChainSession.PartialTransactions where t != null select t;

            var combinedTransaction = account.CombinePartialTransactions(partials.ToArray(), network);
            this.broadcastManager.BroadcastTransactionAsync(combinedTransaction).GetAwaiter().GetResult();
            this.logger.LogInformation("(-)");
        }

        ///<inheritdoc/>
        public async Task<uint256> ProcessCounterChainSession(uint256 sessionId, Money amount, string destinationAddress)
        {
            //todo this method is doing too much. factor some of this into private methods after we added the counterchainid.
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} ProcessCounterChainSession: Amount        - {amount}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} ProcessCounterChainSession: TransactionId - {sessionId}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} ProcessCounterChainSession: Destination   - {destinationAddress}");

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} ProcessCounterChainSession: Session Registered.");

            //Todo: check if this has already been done
            //todo: then we just return the transctionId

            //create the partial transaction template
            var wallet = this.generalPurposeWalletManager.GetWallet(this.federationGatewaySettings.MultiSigWalletName);
            var account = wallet.GetAccountsByCoinType((CoinType)this.network.Consensus.CoinType).First();
            var multiSigAddress = account.MultiSigAddresses.First();

            var destination = BitcoinAddress.Create(destinationAddress, this.network).ScriptPubKey;

            // Encode the sessionId into a string.
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} SessionId encoded bytes length = {sessionId.ToBytes().Length}.");

            // We are the Boss so first I build the multisig transaction template.
            // TODO: The password is currently hardcoded here
            var multiSigContext = new TransactionBuildContext(
                new GeneralPurposeWalletAccountReference(this.federationGatewaySettings.MultiSigWalletName, "account 0"),
                new[] { new Recipient { Amount = amount, ScriptPubKey = destination } }.ToList(),
                "password", sessionId.ToBytes())
            {
                TransactionFee = Money.Coins(0.01m),
                MinConfirmations = 1,
                Shuffle = true,
                MultiSig = multiSigAddress,
                IgnoreVerify = true,
                Sign = false
            };

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} ProcessCounterChainSession: Building Transaction.");
            var templateTransaction = this.generalPurposeWalletTransactionHandler.BuildTransaction(multiSigContext);

            //add my own partial
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} ProcessCounterChainSession: Signing own partial.");
            var counterChainSession = this.VerifySession(sessionId, templateTransaction);

            if (counterChainSession == null) return uint256.One;
            this.MarkSessionAsSigned(counterChainSession);
            var partialTransaction = account.SignPartialTransaction(templateTransaction, wallet, "password", network);

            uint256 bossCard = BossTable.MakeBossTableEntry(sessionId, this.federationGatewaySettings.PublicKey);
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} ProcessCounterChainSession: My bossCard: {bossCard}.");
            this.ReceivePartial(sessionId, partialTransaction, bossCard);

            //now build the requests for the partials
            var requestPartialTransactionPayload = new RequestPartialTransactionPayload(sessionId, templateTransaction);

            //broacast the requests
            var peers = this.connectionManager.ConnectedPeers.ToList();

            foreach (INetworkPeer peer in peers)
            {
                try
                {
                    if (peer.Inbound) continue;
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Broadcasting Partial Transaction Request: SendMessageAsync.");
                    await peer.SendMessageAsync(requestPartialTransactionPayload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            this.logger.LogInformation("(-)");
            return uint256.One;
        }

        ///<inheritdoc/>
        public CounterChainSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate)
        {
            //TODO: This has a critical flaw in the transaction checking. It's not enough to find one ok output. There could be additional rouge outputs.
            //TODO: What are other ways this code can be circumvented?

            var exists = this.sessions.TryGetValue(sessionId, out var counterChainSession);

            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CounterChainSession exists: {exists} sessionId: {sessionId}");

            // We do not have this session.
            if (!exists) return null;

            // Have I already signed this session?
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} HaveISigned:{counterChainSession.HaveISigned}");
            if (counterChainSession.HaveISigned)
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Fatal: the session {sessionId} has already signed a partial transaction.");
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
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Session {sessionId} found the matching destination address.");
                    if (output.Value == amountFromSession)
                    {
                        amountMatches = true;
                        this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Session {sessionId} found the matching amount.");
                    }
                }
            }

            // The addess does not match. exit.
            if (!addressMatches)
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Fatal: The destination address did not match in session {sessionId}.");
                return null;
            }

            // The amount does not match. exit.
            if (!amountMatches)
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Fatal: The amount did not match in session {sessionId}.");
                return null;
            }

            return counterChainSession;
        }

        ///<inheritdoc/>
        public void MarkSessionAsSigned(CounterChainSession session)
        {
            //TODO: this should be locked. the sessions are 30 seconds apart but network conditions could cause a collision.
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} has signed session {session.SessionId}.");
            session.HaveISigned = true;
        }
    }
}

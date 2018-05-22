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
    internal class CounterChainSessionManager : ICounterChainSessionManager
    {
        private IGeneralPurposeWalletManager generalPurposeWalletManager;

        private IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler;

        private IConnectionManager connectionManager;

        private Network network;

        private ConcurrentDictionary<uint256, CounterChainSession> sessions = new ConcurrentDictionary<uint256, CounterChainSession>();

        private FederationGatewaySettings federationGatewaySettings;

        private IBroadcasterManager broadcastManager;

        private IInitialBlockDownloadState initialBlockDownloadState;

        private ConcurrentChain concurrentChain;

        private readonly ILogger logger;

        private IFullNode fullnode;

        public CounterChainSessionManager(ILoggerFactory loggerFactory, IGeneralPurposeWalletManager generalPurposeWalletManager,
            IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler, IConnectionManager connectionManager, Network network,
            FederationGatewaySettings federationGatewaySettings, IInitialBlockDownloadState initialBlockDownloadState, IFullNode fullnode,
            IBroadcasterManager broadcastManager, ConcurrentChain concurrentChain, ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
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

        public void CreateSessionOnCounterChain(uint256 sessionId, Money amount, string destinationAddress)
        {
            // We don't process sessions if our chain is not past IBD.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() CounterChain is in IBD exiting. Height:{this.concurrentChain.Height}.");
                return;
            }
            this.RegisterSession(3, sessionId, amount, destinationAddress);
        }

        private CounterChainSession RegisterSession(int n, uint256 transactionId, Money amount, string destination)
        {
            //todo: not efficient
            var memberFolderManager = new MemberFolderManager(this.federationGatewaySettings.FederationFolder);
            IFederation federation = memberFolderManager.LoadFederation(2, n);

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession transactionId:{transactionId}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession amount:{amount}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession destination:{destination}");

            var partialTransactionSession = new CounterChainSession(this.logger, n, transactionId, federation.GetPublicKeys(this.network.ToChain()), amount, destination);
            this.sessions.AddOrReplace(transactionId, partialTransactionSession);
            return partialTransactionSession;
        }

        public void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: {this.network.ToChain()}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: BossCard - {bossCard}");

            string bc = bossCard.ToString();
            var partialTransactionSession = sessions[sessionId];
            bool hasQuorum = partialTransactionSession.AddPartial(this.federationGatewaySettings.MemberName, partialTransaction, bc);

            if (hasQuorum)
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: Reached Quorum.");
                BroadcastTransaction(partialTransactionSession);
            }
            this.logger.LogInformation("(-)");
        }

        private void BroadcastTransaction(CounterChainSession counterChainSession)
        {
            if (this.fullnode.State == FullNodeState.Disposed || this.fullnode.State == FullNodeState.Disposing)
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Full node disposing during broadcast. Do nothing.");
                return;
            }

            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Combining and Broadcasting transaction.");
            var account = this.generalPurposeWalletManager.GetAccounts("multisig_wallet").First();
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


            var combinedTransaction = account.CombinePartialTransactions(counterChainSession.PartialTransactions);
            this.broadcastManager.BroadcastTransactionAsync(combinedTransaction).GetAwaiter().GetResult();
            this.logger.LogInformation("(-)");
        }

        public async Task<uint256> CreatePartialTransactionSession(uint256 sessionId, Money amount, string destinationAddress)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: Amount        - {amount}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: TransactionId - {sessionId}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: Destination   - {destinationAddress}");

            //var session = this.RegisterSession(3, sessionId, amount, destinationAddress);

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: Session Registered.");

            //Todo: check if this has already been done
            //todo: then we just return the transctionId

            //create the partial transaction template
            var wallet = this.generalPurposeWalletManager.GetWallet("multisig_wallet");
            var account = wallet.GetAccountsByCoinType((CoinType)this.network.Consensus.CoinType).First();

            //TODO: we need to look this up
            var multiSigAddress = account.MultiSigAddresses.First();

            var destination = BitcoinAddress.Create(destinationAddress, this.network).ScriptPubKey;

            // Encode the sessionId into a string.
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} SessionId encoded bytes length = {sessionId.ToBytes().Length}.");

            // We are the Boss so first I build the multisig transaction template.
            var multiSigContext = new TransactionBuildContext(
                new GeneralPurposeWalletAccountReference("multisig_wallet", "account 0"),
                new[] { new Recipient { Amount = amount, ScriptPubKey = destination } }.ToList(),
                "password", sessionId.ToBytes())
            {
                TransactionFee = Money.Coins(0.01m),
                MinConfirmations = 1, // The funds in the multisig address have just been confirmed
                Shuffle = true,
                MultiSig = multiSigAddress,
                IgnoreVerify = true,
                Sign = false
            };

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: Building Transaction.");
            var templateTransaction = this.generalPurposeWalletTransactionHandler.BuildTransaction(multiSigContext);
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: Transaction Built.");

            //add my own partial
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: Signing own partial.");
            var partialTransactionSession = this.VerifySession(sessionId, templateTransaction);
            //todo: this is not right!
            if (partialTransactionSession == null) return uint256.Zero;
            this.MarkSessionAsSigned(partialTransactionSession);
            var partialTransaction = account.SignPartialTransaction(templateTransaction);

            uint256 bossCard = BossTable.MakeBossTableEntry(sessionId, this.federationGatewaySettings.PublicKey);
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: My bossCard: {bossCard}.");
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

        //private CounterChainSession RegisterSession(int n, uint256 transactionId, Money amount, string destination)
        //{
        //    // We only create a partial session once. Before we create the session we must verify the info we receive against our
        //    // own crosschainTransactionInfo to ensure none of the nodes have gone rouge.
        //    // We check....
        //    // 1) The amount matches (check the magnitude and the units).
        //    // 2) The destination address matches.
        //    // 3) That the session exists in our build and broadcast session.
        //    lock (this.locker)
        //    {
        //        //todo: not efficient
        //        var memberFolderManager = new MemberFolderManager(this.federationGatewaySettings.FederationFolder);
        //        IFederation federation = memberFolderManager.LoadFederation(2, n);

        //        bool retrieved = this.sessions.TryGetValue(transactionId, out CounterChainSession counterChainSession);
        //        if (retrieved)
        //        {
        //            this.logger.LogInformation(
        //                $"{this.federationGatewaySettings.MemberName} CounterChainSession exists: {transactionId}. Not adding.");
        //        }
        //        else
        //        {
        //            counterChainSession = new CounterChainSession(this.logger, n, transactionId,
        //                federation.GetPublicKeys(this.network.ToChain()), amount, destination);
        //            this.logger.LogInformation(
        //                $"{this.federationGatewaySettings.MemberName} CounterChainSession adding: {transactionId}.");
        //            this.sessions.AddOrReplace(transactionId, counterChainSession);
        //        }
        //        return counterChainSession;
        //    }
        //}

        public CounterChainSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate)
        {
            var exists = this.sessions.TryGetValue(sessionId, out var partialTransactionSession);
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CounterChainSession exists: {exists} sessionId: {sessionId}");
            this.logger.LogInformation("(-)");

            // We do not have this session, either because we have not yet discovered it or because this is a fake session.
            if (!exists) return null;

            // What does the session expect to receive?
            var scriptPubKeyFromSession = BitcoinAddress.Create(partialTransactionSession.Destination, this.network).ScriptPubKey;
            var amountFromSession = partialTransactionSession.Amount;

            foreach (var output in partialTransactionTemplate.Outputs)
            {
                if (output.ScriptPubKey == scriptPubKeyFromSession)
                {
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Found it!");
                    if (output.Value == amountFromSession)
                        this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Amount matches!");
                }
            }

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} HaveISigned:{partialTransactionSession.HaveISigned}");

            //if (counterChainSession.HaveISigned)
            //    throw new ArgumentException($"Fatal: the session {sessionId} has already signed a partial transaction.");

            //if (amount != counterChainSession.Amount)
            //    throw new ArgumentException($"Fatal the session amount does not match chain.");

            //if (destination != counterChainSession.Destination)
            //    throw new ArgumentException($"Fatal the session destination does not match chain.");
            return partialTransactionSession;
        }

        public void MarkSessionAsSigned(CounterChainSession session)
        {
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} has signed session {session.SessionId}.");
            session.HaveISigned = true;
        }
    }
}

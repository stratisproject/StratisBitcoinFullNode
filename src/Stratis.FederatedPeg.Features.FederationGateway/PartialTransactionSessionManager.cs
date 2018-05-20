using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using System.Threading;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway) 

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class PartialTransactionSession
    {
        private Transaction[] partialTransactions;

        public uint256 SessionId { get; }
        public Money Amount { get; }
        public string Destination { get; }

        // todo: we can remove this if we just use a list for the partials
        private BossTable bossTable;

        public bool HasReachedQuorum { get; private set; }

        public Transaction[] PartialTransactions => this.partialTransactions;

        private ILogger logger;

        public bool HaveISigned { get; set; } = false;

        public PartialTransactionSession(ILogger logger, int federationSize, uint256 sessionId, string[] addresses, Money amount, string destination)
        {
            this.logger = logger;
            this.partialTransactions = new Transaction[federationSize];
            this.SessionId = sessionId;
            this.Amount = amount;
            this.Destination = destination;
            this.bossTable = new BossTableBuilder().Build(sessionId, addresses);
        }

        internal bool AddPartial(string memberName, Transaction partialTransaction, string bossCard)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{memberName} Adding Partial to BuildAndBroadcastSession.");

            // Insert the partial transaction in the session.
            int positionInTable = 0;
            for (; positionInTable < 3; ++positionInTable )
                if (bossCard == bossTable.BossTableEntries[positionInTable])
                    break;
            this.partialTransactions[positionInTable] = partialTransaction;

            // Have we reached Quorum?
            this.HasReachedQuorum = this.CountPartials() >= 2;

            // Output parts info.
            this.logger.LogInformation($"{memberName} New Partials");
            this.logger.LogInformation($"{memberName} ---------");
            foreach (var p in partialTransactions)
            {
                if (p == null)
                    this.logger.LogInformation($"{memberName} null");
                else
                    this.logger.LogInformation($"{memberName} {p?.ToHex()}");
            }
            this.logger.LogInformation($"{memberName} ---------");
            this.logger.LogInformation($"{memberName} HasQuorum: {this.HasReachedQuorum}");
            this.logger.LogInformation("(-)");
            // End output. 

            return this.HasReachedQuorum;
        }

        private int CountPartials()
        {
            int positionInTable = 0;
            int count = 0;
            for (; positionInTable < 3; ++positionInTable)
                if (partialTransactions[positionInTable] != null)
                    ++count;
            return count;
        }
    }

    internal class PartialTransactionSessionManager : IPartialTransactionSessionManager
    {
        private const int BlockSecurityDelay = 0;

        private IGeneralPurposeWalletManager generalPurposeWalletManager;

        private IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler;

        private IConnectionManager connectionManager;

        private Network network;

        private ConcurrentDictionary<uint256, PartialTransactionSession> sessions = new ConcurrentDictionary<uint256, PartialTransactionSession>();

        private FederationGatewaySettings federationGatewaySettings;

        private IBroadcasterManager broadcastManager;

        private IInitialBlockDownloadState initialBlockDownloadState;

        private ConcurrentChain concurrentChain;

        private readonly ILogger logger;

        private Timer actionTimer;

        private ConcurrentDictionary<uint256, BuildAndBroadcastSession> buildAndBroadcastSessions = new ConcurrentDictionary<uint256, BuildAndBroadcastSession>();

        private object locker = new object();

        private TimeSpan sessionRunInterval = new TimeSpan(hours: 0, minutes: 0, seconds: 30);

        private ICrossChainTransactionAuditor crossChainTransactionAuditor;

        public PartialTransactionSessionManager(ILoggerFactory loggerFactory, IGeneralPurposeWalletManager generalPurposeWalletManager,
            IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler, IConnectionManager connectionManager, Network network,
            FederationGatewaySettings federationGatewaySettings, IInitialBlockDownloadState initialBlockDownloadState,
            IBroadcasterManager broadcastManager, ConcurrentChain concurrentChain, ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.generalPurposeWalletManager = generalPurposeWalletManager;
            this.generalPurposeWalletTransactionHandler = generalPurposeWalletTransactionHandler;
            this.connectionManager = connectionManager;
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
            this.broadcastManager = broadcastManager;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.concurrentChain = concurrentChain;
            this.crossChainTransactionAuditor = crossChainTransactionAuditor;
        }

        public void Initialize()
        {
            this.actionTimer = new Timer(async (o) =>
            {
                await this.RunSessionsAsync().ConfigureAwait(false);
            }, null, 0, (int)this.sessionRunInterval.TotalMilliseconds);
        }

        public void Dispose()
        {
            this.actionTimer?.Dispose();
        }

        // Creates the BuildAndBroadcast session.
        // A session is added when the CrossChainTransactionMonitor identifies a transaction that needs to be completed cross chain.
        public uint256 CreateBuildAndBroadcastSession(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            var buildAndBroadcastSession = new BuildAndBroadcastSession(
                DateTime.Now,
                crossChainTransactionInfo.TransactionHash,
                crossChainTransactionInfo.Amount,
                crossChainTransactionInfo.DestinationAddress,
                this.network.ToChain(),
                this.federationGatewaySettings.FederationFolder,
                this.federationGatewaySettings.PublicKey
            );
                
            buildAndBroadcastSession.CrossChainTransactionInfo = crossChainTransactionInfo;
            this.buildAndBroadcastSessions.TryAdd(buildAndBroadcastSession.SessionId, buildAndBroadcastSession);

            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} BuildAndBroadcastSession  added: {buildAndBroadcastSession}");
            this.logger.LogInformation("(-)");

            return this.CreateSessionOnCounterChain(this.federationGatewaySettings.CounterChainApiPort,
                crossChainTransactionInfo.TransactionHash,
                crossChainTransactionInfo.Amount, crossChainTransactionInfo.DestinationAddress);
        }

        // Calls into the counter chain and registers the session there.
        private uint256 CreateSessionOnCounterChain(int apiPortForSidechain, uint256 transactionId, Money amount, string destination)
        {
            var createPartialTransactionSessionRequest = new CreatePartialTransactionSessionRequest
            {
                SessionId = transactionId,
                Amount = amount.ToString(),
                DestinationAddress = destination
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/FederationGateway/create-sessiononcounterchain");
                var request = new JsonContent(createPartialTransactionSessionRequest);
                var httpResponseMessage = client.PostAsync(uri, request).Result;
                string json = httpResponseMessage.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<uint256>(json, new UInt256JsonConverter());
            }
        }

        public uint256 CreateSessionOnCounterChain(uint256 sessionId, Money amount, string destinationAddress)
        {
            // We don't process sessions if our chain is not past IBD.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() CounterChain is in IBD exiting. Height:{this.concurrentChain.Height}.");
                return uint256.Zero;
            }

            this.RegisterSession(3, sessionId, amount, destinationAddress);
            return uint256.Zero;
        }

        private PartialTransactionSession RegisterSession(int n, uint256 transactionId, Money amount, string destination)
        {
            //todo: not efficient
            var memberFolderManager = new MemberFolderManager(this.federationGatewaySettings.FederationFolder);
            IFederation federation = memberFolderManager.LoadFederation(2, n);

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession transactionId:{transactionId}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession amount:{amount}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RegisterSession destination:{destination}");

            var partialTransactionSession = new PartialTransactionSession(this.logger, n, transactionId, federation.GetPublicKeys(this.network.ToChain()), amount, destination);
            this.sessions.AddOrReplace(transactionId, partialTransactionSession);
            return partialTransactionSession;
        }

        private async Task RunSessionsAsync()
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync()");

            // We don't process sessions if our chain is not past IBD.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() Monitor chain is in IBD exiting. Height:{this.concurrentChain.Height}.");
                return;
            }

            //foreach (var buildAndBroadcastSession in this.buildAndBroadcastSessions.Values)
            //{
                // Don't do anything if we are within the block security delay.
            //    if (!this.IsBeyondBlockSecurityDelay(buildAndBroadcastSession)) return;
            //}

            foreach (var buildAndBroadcastSession in this.buildAndBroadcastSessions.Values)
            {
                lock (this.locker)
                {
                    var time = DateTime.Now;

                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() MyBossCard:{buildAndBroadcastSession.BossCard}");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} At {time} AmITheBoss: {buildAndBroadcastSession.AmITheBoss(time)} WhoHoldsTheBossCard: {buildAndBroadcastSession.WhoHoldsTheBossCard(time)}");

                    // We don't start the process until we are beyond the BlockSecurityDelay.
                    if (buildAndBroadcastSession.Status == BuildAndBroadcastSession.SessionStatus.Created
                        && buildAndBroadcastSession.AmITheBoss(time) && IsBeyondBlockSecurityDelay(buildAndBroadcastSession))
                    {
                        this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() - Session State: Created -> Requesting.");
                        buildAndBroadcastSession.Status = BuildAndBroadcastSession.SessionStatus.Requesting;
                    }
                }

                if (buildAndBroadcastSession.Status == BuildAndBroadcastSession.SessionStatus.Requesting)
                {
                    buildAndBroadcastSession.Status = BuildAndBroadcastSession.SessionStatus.RequestSending;
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() - CreateBuildAndBroadcastSession.");
                    var requestPartialsResult = await CreateBuildAndBroadcastSession(this.federationGatewaySettings.CounterChainApiPort, buildAndBroadcastSession.Amount, buildAndBroadcastSession.DestinationAddress, buildAndBroadcastSession.SessionId).ConfigureAwait(false);

                    if (requestPartialsResult == uint256.Zero)
                    {
                        this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() - Session State: Requesting -> Requested.");
                        buildAndBroadcastSession.Status = BuildAndBroadcastSession.SessionStatus.Requested;
                    }
                    else
                    {
                        this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() - Completing Session {requestPartialsResult}.");
                        buildAndBroadcastSession.Complete(requestPartialsResult);
                        buildAndBroadcastSession.CrossChainTransactionInfo.CrossChainTransactionId = requestPartialsResult;
                    }
                }
            }
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

        private void BroadcastTransaction(PartialTransactionSession partialTransactionSession)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Combining and Broadcasting transaction.");
            var account = this.generalPurposeWalletManager.GetAccounts("multisig_wallet").First();
            if (account == null)
            {
                this.logger.LogInformation("InvalidAccount from GPWallet.");
                return;
            }
            var combinedTransaction = account.CombinePartialTransactions(partialTransactionSession.PartialTransactions);
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

       //private PartialTransactionSession RegisterSession(int n, uint256 transactionId, Money amount, string destination)
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

        //        bool retrieved = this.sessions.TryGetValue(transactionId, out PartialTransactionSession partialTransactionSession);
        //        if (retrieved)
        //        {
        //            this.logger.LogInformation(
        //                $"{this.federationGatewaySettings.MemberName} PartialTransactionSession exists: {transactionId}. Not adding.");
        //        }
        //        else
        //        {
        //            partialTransactionSession = new PartialTransactionSession(this.logger, n, transactionId,
        //                federation.GetPublicKeys(this.network.ToChain()), amount, destination);
        //            this.logger.LogInformation(
        //                $"{this.federationGatewaySettings.MemberName} PartialTransactionSession adding: {transactionId}.");
        //            this.sessions.AddOrReplace(transactionId, partialTransactionSession);
        //        }
        //        return partialTransactionSession;
        //    }
        //}

        public PartialTransactionSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate)
        {
            var exists = this.sessions.TryGetValue(sessionId, out var partialTransactionSession);
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} PartialTransactionSession exists: {exists} sessionId: {sessionId}");
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

            //if (partialTransactionSession.HaveISigned)
            //    throw new ArgumentException($"Fatal: the session {sessionId} has already signed a partial transaction.");

            //if (amount != partialTransactionSession.Amount)
            //    throw new ArgumentException($"Fatal the session amount does not match chain.");

            //if (destination != partialTransactionSession.Destination)
            //    throw new ArgumentException($"Fatal the session destination does not match chain.");
            return partialTransactionSession;
        }

        private bool IsBeyondBlockSecurityDelay(BuildAndBroadcastSession buildAndBroadcastSession)
        {
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} IsBeyondBlockSecurityDelay() ");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} IsBeyondBlockSecurityDelay() SessionBlock: {buildAndBroadcastSession.CrossChainTransactionInfo.BlockNumber}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} IsBeyondBlockSecurityDelay() BlockDelay: {BlockSecurityDelay}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} IsBeyondBlockSecurityDelay() Height: {this.concurrentChain.Tip.Height}");

            return buildAndBroadcastSession.CrossChainTransactionInfo.BlockNumber >=
                   BlockSecurityDelay + this.concurrentChain.Tip.Height;
        }

        //Calls into the counter chain and sets off the process to build the multi-sig transaction.
        private async Task<uint256> CreateBuildAndBroadcastSession(int apiPortForSidechain, Money amount, string destination, uint256 transactionId)
        {
            var createPartialTransactionSessionRequest = new CreatePartialTransactionSessionRequest
            {
                SessionId = transactionId,
                Amount = amount.ToString(),
                DestinationAddress = destination
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri(
                    $"http://localhost:{apiPortForSidechain}/api/FederationGateway/create-buildbroadcast-session");
                var request = new JsonContent(createPartialTransactionSessionRequest);
                var httpResponseMessage = await client.PostAsync(uri, request);
                string json = await httpResponseMessage.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<uint256>(json, new UInt256JsonConverter());
            }
        }

        public void MarkSessionAsSigned(PartialTransactionSession session)
        {
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} has signed session {session.SessionId}.");
            session.HaveISigned = true;
        }
    }
}

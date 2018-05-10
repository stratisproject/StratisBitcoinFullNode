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
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.P2P.Peer;
using System.Threading;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway) 

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    class PartialTransactionSession
    {
        private Transaction[] partialTransactions;
        private uint256 sessionId;

        private BossTable bossTable;

        public bool HasReachedQuorum { get; private set; }

        public Transaction[] PartialTransactions => this.partialTransactions;

        private ILogger logger;

        public PartialTransactionSession(ILogger logger, int federationSize, uint256 sessionId, string[] addresses)
        {
            this.logger = logger;
            this.partialTransactions = new Transaction[federationSize];
            this.sessionId = sessionId;
            this.bossTable = new BossTableBuilder().Build(sessionId, addresses);
        }

        internal bool AddPartial(Transaction partialTransaction, string bossCard)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"HasQuorum: {this.HasReachedQuorum}");

            this.logger.LogInformation("AddPartial");
            this.logger.LogInformation("");
            this.logger.LogInformation("BossTableBuilder");
            this.logger.LogInformation("---------");

            foreach (string bc in bossTable.BossTableEntries)
                this.logger.LogInformation(bc);

            this.logger.LogInformation("---------");

            this.logger.LogInformation($"AddPartial: Incoming bossCard - {bossCard}");

            this.logger.LogInformation("Partials Before");
            this.logger.LogInformation("---------");

            foreach (var p in partialTransactions)
            {
                if (p == null)
                    this.logger.LogInformation("null");
                else
                    this.logger.LogInformation($"{p?.ToHex()}");
            }

            this.logger.LogInformation("---------");

            int positionInTable = 0;
            for (; positionInTable < 3; ++positionInTable )
                if (bossCard == bossTable.BossTableEntries[positionInTable])
                    break;
            this.partialTransactions[positionInTable] = partialTransaction;

            this.logger.LogInformation("Partials After");
            this.logger.LogInformation("---------");

            foreach (var p in partialTransactions)
            {
                if (p == null)
                    this.logger.LogInformation("null");
                else
                    this.logger.LogInformation($"{p?.ToHex()}");
            }
            this.logger.LogInformation("---------");

            this.HasReachedQuorum = this.CountPartials() >=2;
            this.logger.LogInformation($"HasQuorum: {this.HasReachedQuorum}");
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
        private IGeneralPurposeWalletManager generalPurposeWalletManager;
        private IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler;
        private IConnectionManager connectionManager;

        private Network network;

        private ConcurrentDictionary<uint256, PartialTransactionSession> sessions = new ConcurrentDictionary<uint256, PartialTransactionSession>();

        private FederationGatewaySettings federationGatewaySettings;

        private IBroadcasterManager broadcastManager;

        private readonly ILogger logger;

        //from babsman
        private Timer actionTimer;

        private ConcurrentBag<BuildAndBroadcastSession> buildAndBroadcastSessions = new ConcurrentBag<BuildAndBroadcastSession>();

        private object locker = new object();

        private TimeSpan sessionRunInterval = new TimeSpan(hours: 0, minutes: 0, seconds: 30);

        public PartialTransactionSessionManager(ILoggerFactory loggerFactory, IGeneralPurposeWalletManager generalPurposeWalletManager,
            IGeneralPurposeWalletTransactionHandler generalPurposeWalletTransactionHandler, IConnectionManager connectionManager, Network network,
            FederationGatewaySettings federationGatewaySettings,
            IBroadcasterManager broadcastManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.generalPurposeWalletManager = generalPurposeWalletManager;
            this.generalPurposeWalletTransactionHandler = generalPurposeWalletTransactionHandler;
            this.connectionManager = connectionManager;
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
            this.broadcastManager = broadcastManager;
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

        public void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard)
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: {this.network.ToChain()}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Receive Partial: BossCard - {bossCard}");

            string bc = bossCard.ToString();
            var partialTransactionSession = sessions[sessionId];
            bool hasQuorum = partialTransactionSession.AddPartial(partialTransaction, bc);

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
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Combing and Broadcasting transactions.");
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
            
            var session = this.RegisterSession(3, sessionId);

            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CreatePartialTransactionSession: Session Registered.");

            //Todo: check if this has already been done
            //todo: then we just return the transctionId

            //create the partial transaction template
            var wallet = this.generalPurposeWalletManager.GetWallet("multisig_wallet");
            var account = wallet.GetAccountsByCoinType((CoinType)this.network.Consensus.CoinType).First();

            //TODO: we need to look this up
            var multiSigAddress = account.MultiSigAddresses.First();

            var destination = BitcoinAddress.Create(destinationAddress, this.network).ScriptPubKey;

            //we are the Boss so first I build the multisig transation template.
            var multiSigContext = new TransactionBuildContext(
                new GeneralPurposeWalletAccountReference("multisig_wallet", "account 0"),
                new[] { new Recipient { Amount = amount, ScriptPubKey = destination } }.ToList(),
                "password")
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

        private PartialTransactionSession RegisterSession(int n, uint256 transactionId)
        {
            //todo: not efficient
            var memberFolderManager = new MemberFolderManager(this.federationGatewaySettings.FederationFolder);
            IFederation federation = memberFolderManager.LoadFederation(2, n);
            
            var partialTransactionSession = new PartialTransactionSession(this.logger, n, transactionId, federation.GetPublicKeys(this.network.ToChain()));
            this.sessions.AddOrReplace(transactionId, partialTransactionSession);
            return partialTransactionSession;
        }

        public void CreateBuildAndBroadcastSession(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            var buildAndBroadcastSession = new BuildAndBroadcastSession(this.network.ToChain(),
                DateTime.Now,
                this.federationGatewaySettings.FederationFolder,
                crossChainTransactionInfo.TransactionHash,
                this.federationGatewaySettings.PublicKey,
                crossChainTransactionInfo.DestinationAddress,
                crossChainTransactionInfo.Amount);
            buildAndBroadcastSession.CrossChainTransactionInfo = crossChainTransactionInfo;
            this.buildAndBroadcastSessions.Add(buildAndBroadcastSession);

            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} BuildAndBroadcastSession  added: {this.network.ToChain()}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} BuildAndBroadcastSession  added: Amount                  - {buildAndBroadcastSession.Amount}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} BuildAndBroadcastSession  added: Destination             - {buildAndBroadcastSession.DestinationAddress}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} BuildAndBroadcastSession  added: SessionId               - {buildAndBroadcastSession.SessionId}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} BuildAndBroadcastSession  added: Status                  - {buildAndBroadcastSession.Status}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CrossChainTransactionInfo added: Amount                  - {buildAndBroadcastSession.CrossChainTransactionInfo.Amount}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CrossChainTransactionInfo added: BlockHash               - {buildAndBroadcastSession.CrossChainTransactionInfo.BlockHash}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CrossChainTransactionInfo added: BlockNumber             - {buildAndBroadcastSession.CrossChainTransactionInfo.BlockNumber}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CrossChainTransactionInfo added: CrossChainTransactionId - {buildAndBroadcastSession.CrossChainTransactionInfo.CrossChainTransactionId}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CrossChainTransactionInfo added: DestinationAddress      - {buildAndBroadcastSession.CrossChainTransactionInfo.DestinationAddress}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} CrossChainTransactionInfo added: TransactionHash         - {buildAndBroadcastSession.CrossChainTransactionInfo.TransactionHash}");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} BuildAndBroacastSession   added: SessionCount            - {this.sessions.Count}");
            this.logger.LogInformation("()");
        }

        private async Task RunSessionsAsync()
        {
            this.logger.LogInformation("()");
            this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync()");

            foreach (var buildAndBroadcastSession in this.buildAndBroadcastSessions)
            {
                lock (this.locker)
                {
                    var time = DateTime.Now;

                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() MyBossCard:{buildAndBroadcastSession.BossCard}");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} At {time} AmITheBoss: {buildAndBroadcastSession.AmITheBoss(time)} WhoHoldsTheBossCard: {buildAndBroadcastSession.WhoHoldsTheBossCard(time)}");

                    if (buildAndBroadcastSession.Status == BuildAndBroadcastSession.SessionStatus.Created
                        && buildAndBroadcastSession.AmITheBoss(time))
                    {
                        this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() - Session State: Created -> Requesting.");
                        buildAndBroadcastSession.Status = BuildAndBroadcastSession.SessionStatus.Requesting;
                    }
                }

                if (buildAndBroadcastSession.Status == BuildAndBroadcastSession.SessionStatus.Requesting)
                {
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RunSessionsAsync() - RequestingPartials.");
                    var requestPartialsResult = await CreateBuildAndBroadcastSession(this.federationGatewaySettings.CounterChainApiPort, buildAndBroadcastSession.Amount, buildAndBroadcastSession.DestinationAddress, buildAndBroadcastSession.SessionId);

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
    }
}

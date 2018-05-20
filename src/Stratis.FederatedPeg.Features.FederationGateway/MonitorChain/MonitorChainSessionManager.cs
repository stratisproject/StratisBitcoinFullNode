using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.MonitorChain
{
    public class MonitorChainSessionManager : IMonitorChainSessionManager
    {
        private const int BlockSecurityDelay = 0;

        private readonly ILogger logger;

        private Timer actionTimer;

        private TimeSpan sessionRunInterval = new TimeSpan(hours: 0, minutes: 0, seconds: 30);

        private Network network;

        private FederationGatewaySettings federationGatewaySettings;

        private ConcurrentDictionary<uint256, BuildAndBroadcastSession> buildAndBroadcastSessions = new ConcurrentDictionary<uint256, BuildAndBroadcastSession>();

        private IInitialBlockDownloadState initialBlockDownloadState;

        private object locker = new object();

        private ConcurrentChain concurrentChain;

        private ICrossChainTransactionAuditor crossChainTransactionAuditor;

        public MonitorChainSessionManager(
            ILoggerFactory loggerFactory,
            FederationGatewaySettings federationGatewaySettings,
            Network network,
            ConcurrentChain concurrentChain,
            IInitialBlockDownloadState initialBlockDownloadState,
            ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
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
    }
}

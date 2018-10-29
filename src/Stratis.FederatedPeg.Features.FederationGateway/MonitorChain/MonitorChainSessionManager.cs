using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway.MonitorChain
{
    /// <summary>
    /// The MonitorChainSessionManager creates a session that is used to communicate transaction info
    /// to the CounterChain.  When a federation member is asked to sign a transaction, the details of
    /// that transaction (amount and destination address) are checked against this session data. 
    /// 
    /// A federation member runs full node versions of both chains that trust each other and communicate
    /// through a local API.  However, other federation members are not immediately trusted and requests
    /// to sign a partial transaction are checked for validity before the transaction is signed.  
    /// This means checking:
    ///   a) That a session exists (prevents a rouge gateway from generating fake transactions).
    ///   b) That the address matches (prevents a rouge gateway from diverting funds).
    ///   c) That the amount matches (enforces exactly matching of debits and credits across the two chains).
    ///   d) It is also necessary to check that a federation member has not already signed the transaction.
    ///      The federation gateway ensures that transactions are only ever signed once. (If a rouge
    ///      federation gateway circulates multiple transaction templates with difference spending inputs this
    ///      rule ensures that these are not signed.)
    /// 
    /// Both nodes need to be fully synced before any processing is done and nodes only process cross chain transactions from 
    /// new blocks. They never look backwards to do any corrective processing of transactions that may have failed. It is assumed
    /// that the other gateways have reached a quorum on those transactions. Should that have not happened then corrective action
    /// involves an offline agreement between nodes to post any corrective measures. Processing is never done if either node is
    /// in initialBlockDownload.
    /// 
    /// Gateways monitor the Mainchain for deposit transactions and the Sidechain for withdrawals.
    /// Deposits are an exact mirror image of withdrawals and the exact same process (and code) is used.
    /// We have therefore, a MonitorChain and a CounterChain. For a deposit, the MonitorChain is
    /// Mainchain and the CounterChain is our Sidechain. For withdrawals the MonitorChain is the
    /// Sidechain and the CounterChain is Mainchain.
    /// </summary>
    public class MonitorChainSessionManager : IMonitorChainSessionManager
    {
        // The number of blocks to wait before we create the session.
        // This is typically used for reorgs protection.
        private const int BlockSecurityDelay = 0;

        private readonly ILogger logger;

        // The time between sessions.
        private readonly TimeSpan sessionRunInterval = new TimeSpan(hours: 0, minutes: 0, seconds: 30);
        
        // Timer used to trigger session processing.
        private Timer actionTimer;
        
        private readonly FederationGatewaySettings federationGatewaySettings;

        private readonly ConcurrentDictionary<int, MonitorChainSession> monitorSessions = new ConcurrentDictionary<int, MonitorChainSession>();

        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly ConcurrentChain concurrentChain;

        // The auditor can capture the details of the transactions that the monitor discovers.
        private readonly ICrossChainTransactionAuditor crossChainTransactionAuditor;

        public MonitorChainSessionManager(
            ILoggerFactory loggerFactory,
            FederationGatewaySettings federationGatewaySettings,
            ConcurrentChain concurrentChain,
            IInitialBlockDownloadState initialBlockDownloadState,
            ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.federationGatewaySettings = federationGatewaySettings;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.concurrentChain = concurrentChain;
            this.crossChainTransactionAuditor = crossChainTransactionAuditor;
        }

        public void Initialize()
        {
            // We don't know how regular blocks will be on the sidechain so instead of
            // processing sessions on new blocks we use a timer.
            this.actionTimer = new Timer(async (o) =>
            {
                await this.RunSessionAsync().ConfigureAwait(false);
            }, null, 0, (int)this.sessionRunInterval.TotalMilliseconds);
        }

        public void Dispose()
        {
            this.actionTimer?.Dispose();
        }

        // A session is added when the CrossChainTransactionMonitor identifies a transaction that needs to be completed cross chain.
        public void RegisterMonitorSession(MonitorChainSession monitorSession)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(monitorSession.BlockNumber), monitorSession.BlockNumber);
            
            this.monitorSessions.TryAdd(monitorSession.BlockNumber, monitorSession);
        }

        // Calls into the counter chain and registers the session there.
        public void CreateSessionOnCounterChain(int apiPortForSidechain, MonitorChainSession monitorChainSession)
        {
            
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(apiPortForSidechain), apiPortForSidechain, nameof(monitorChainSession.BlockNumber), monitorChainSession.BlockNumber, "Transactions Count", monitorChainSession.CrossChainTransactions.Count);

            var createCounterChainSessionRequest = new CreateCounterChainSessionRequest
            {
                BlockHeight = monitorChainSession.BlockNumber,
                CounterChainTransactionInfos = monitorChainSession.CrossChainTransactions.Select(t => new CounterChainTransactionInfoRequest
                {
                    TransactionHash = t.TransactionHash,
                    DestinationAddress = t.DestinationAddress,
                    Amount = t.Amount.ToString()
                }).ToList()
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri($"http://localhost:{apiPortForSidechain}/api/FederationGateway/create-session-oncounterchain");
                var request = new JsonContent(createCounterChainSessionRequest);
                
                try
                {
                    var httpResponseMessage = client.PostAsync(uri, request).ConfigureAwait(false).GetAwaiter().GetResult();
                    string json = httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Error occurred when calling /api/FederationGateway/create-session-oncounterchain: {0}", e.Message);
                }
            }

            this.logger.LogTrace("(-)");
        }

        private async Task RunSessionAsync()
        {
            this.logger.LogTrace("()");
            this.logger.LogInformation("RunSessionAsync()");

            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation("RunSessionAsync() Monitor chain is in IBD exiting. Height:{0}.", this.concurrentChain.Height);
                return;
            }

            foreach (var monitorChainSession in this.monitorSessions.Values)
            {
                if (monitorChainSession.Status == SessionStatus.Completed) continue;

                if (!IsBeyondBlockSecurityDelay(monitorChainSession)) continue;

                var time = DateTime.Now;

                this.logger.LogInformation($"Session status: {0} with CounterChainTransactionId: {1}.", 
                    monitorChainSession.Status.ToString(), monitorChainSession.CounterChainTransactionId);
                
                this.logger.LogInformation("RunSessionAsync() MyBossCard: {0}", monitorChainSession.BossCard);
                this.logger.LogInformation("At {0} AmITheBoss: {1} WhoHoldsTheBossCard: {2}", 
                    time, monitorChainSession.AmITheBoss(time), monitorChainSession.WhoHoldsTheBossCard(time));
                    
                if (!monitorChainSession.AmITheBoss(time) &&
                    monitorChainSession.Status != SessionStatus.Requested) continue;

                if (monitorChainSession.Status == SessionStatus.Created)
                    // Session was newly created and I'm the boss so I start the process.
                    monitorChainSession.Status = SessionStatus.Requesting;

                // We call this for an incomplete session whenever we become the boss.
                // On the counterchain this will either start the process or just inform us
                // the the session completed already (and give us the CounterChainTransactionId).
                // We we were already the boss the status will be Requested and we will Process
                // to get the CounterChainTransactionId.
                var result = await ProcessSessionOnCounterChain(this.federationGatewaySettings.SourceChainApiPort, monitorChainSession).ConfigureAwait(false);

                if (monitorChainSession.Status == SessionStatus.Requesting)
                    monitorChainSession.Status = SessionStatus.Requested;

                if (result != uint256.Zero)
                {
                    monitorChainSession.Complete(result);

                    if(result == uint256.One) this.logger.LogInformation("Session for block {0} failed.", monitorChainSession.BlockNumber);
                    else this.logger.LogInformation("RunSessionAsync() - Completing Session {0}.", result);

                    foreach (var trx in monitorChainSession.CrossChainTransactions)
                    {
                        this.crossChainTransactionAuditor.AddCounterChainTransactionId(trx.TransactionHash, result);
                    }
                    this.crossChainTransactionAuditor.Commit();
                }

                this.logger.LogInformation("Status: {0} with result: {1}.", monitorChainSession.Status, result);
            }
        }

        private bool IsBeyondBlockSecurityDelay(MonitorChainSession monitorChainSession)
        {
            this.logger.LogInformation("() Session started at block {0}, block security delay {1}, current block height {2}, waiting for {3} more blocks",
                monitorChainSession.BlockNumber, BlockSecurityDelay, this.concurrentChain.Tip.Height, 
                monitorChainSession.BlockNumber + BlockSecurityDelay - this.concurrentChain.Tip.Height );
           
            return this.concurrentChain.Tip.Height >= (monitorChainSession.BlockNumber + BlockSecurityDelay);
        }

        // Calls into the counter chain and sets off the process to build the multi-sig transaction.
        private async Task<uint256> ProcessSessionOnCounterChain(int apiPortForSidechain, MonitorChainSession monitorChainSession)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(apiPortForSidechain), apiPortForSidechain, nameof(monitorChainSession.BlockNumber), monitorChainSession.BlockNumber, "Transactions Count", monitorChainSession.CrossChainTransactions.Count);

            var createCounterChainSessionRequest = new CreateCounterChainSessionRequest
            {
                BlockHeight = monitorChainSession.BlockNumber,
                CounterChainTransactionInfos = monitorChainSession.CrossChainTransactions.Select(t => new CounterChainTransactionInfoRequest
                {
                    TransactionHash = t.TransactionHash,
                    DestinationAddress = t.DestinationAddress,
                    Amount = t.Amount.ToString()
                }).ToList()
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri($"http://localhost:{apiPortForSidechain}/api/FederationGateway/process-session-oncounterchain");
                var request = new JsonContent(createCounterChainSessionRequest);
                
                try
                {
                    var httpResponseMessage = await client.PostAsync(uri, request);
                    this.logger.LogInformation("Response: {0}", await httpResponseMessage.Content.ReadAsStringAsync());

                    if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return uint256.One;
                    }

                    if (httpResponseMessage.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        this.logger.LogError("Error occurred when calling /api/FederationGateway/process-session-oncounterchain: {0}-{1}", httpResponseMessage.StatusCode, httpResponseMessage.ToString());
                        return uint256.Zero;
                    }

                    string json = await httpResponseMessage.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<uint256>(json, new UInt256JsonConverter());
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Error occurred when calling /api/FederationGateway/process-session-oncounterchain: {0}", e.Message);
                    return uint256.Zero;
                }
            }
        }
    }
}


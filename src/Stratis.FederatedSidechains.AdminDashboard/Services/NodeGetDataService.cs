using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NUglify.Helpers;
using Stratis.FederatedSidechains.AdminDashboard.Entities;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.FederatedSidechains.AdminDashboard.Services
{
    public abstract class NodeGetDataService
    {
        public NodeStatus NodeStatus { get; set; }
        public List<LogRule> LogRules { get; set; }
        public int RawMempool { get; set; } = 0;
        public string BestHash { get; set; } = String.Empty;

        public ApiResponse StatusResponse { get; set; }
        public ApiResponse FedInfoResponse { get; set; }
        public List<PendingPoll> PendingPolls { get; set; }
        public int FedMemberCount { get; set; }
        public NodeDashboardStats NodeDashboardStats { get; set; }
        public string MiningPubKey { get; set; }

        protected const int STRATOSHI = 100_000_000;
        protected readonly string miningKeyFile = String.Empty;

        private ApiRequester _apiRequester;
        private string _endpoint;
        private readonly ILogger<NodeGetDataService> logger;
        protected readonly bool isMainnet = true;

        public NodeGetDataService(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory, string env)
        {
            _apiRequester = apiRequester;
            _endpoint = endpoint;
            this.logger = loggerFactory.CreateLogger<NodeGetDataService>();
            this.isMainnet = env != NodeEnv.TestNet;

            try
            {
                miningKeyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StratisNode", "cirrus", this.isMainnet ? "CirrusMain" : "CirrusTest",
                    "federationKey.dat");
                try
                {
                    using (FileStream readStream = File.OpenRead(miningKeyFile))
                    {
                        var privateKey = new Key();
                        var stream = new BitcoinStream(readStream, false);
                        stream.ReadWrite(ref privateKey);
                        this.MiningPubKey = Encoders.Hex.EncodeData(privateKey.PubKey.ToBytes());
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, $"Failed to read file {miningKeyFile}");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Failed to get APPDATA");
            }
        }

        public virtual async Task<NodeGetDataService> Update()
        {
            NodeDashboardStats = await UpdateDashboardStats();
            NodeStatus = await UpdateNodeStatus();
            LogRules = await UpdateLogRules();
            RawMempool = await UpdateMempool();
            BestHash = await UpdateBestHash();
            return this;
        }

        protected async Task<NodeStatus> UpdateNodeStatus()
        {
            NodeStatus nodeStatus = new NodeStatus();
            try
            {
                StatusResponse = await _apiRequester.GetRequestAsync(_endpoint, "/api/Node/status");
                nodeStatus.BlockStoreHeight = StatusResponse.Content.blockStoreHeight;
                nodeStatus.ConsensusHeight = StatusResponse.Content.consensusHeight;
                string upTimeLargePrecion = StatusResponse.Content.runningTime;
                nodeStatus.Uptime = upTimeLargePrecion.Split('.')[0];
                nodeStatus.State = StatusResponse.Content.state;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update node status");
            }

            return nodeStatus;
        }

        protected async Task<List<LogRule>> UpdateLogRules()
        {
            List<LogRule> responseLog = new List<LogRule>();
            try
            {
                ApiResponse response = await _apiRequester.GetRequestAsync(_endpoint, "/api/Node/logrules");
                responseLog = JsonConvert.DeserializeObject<List<LogRule>>(response.Content.ToString());
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get log rules");
            }

            return responseLog;
        }

        protected async Task<int> UpdateMempool()
        {
            int mempoolSize = 0;
            try
            {
                ApiResponse response = await _apiRequester.GetRequestAsync(_endpoint, "/api/Mempool/getrawmempool");
                mempoolSize = response.Content.Count;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get mempool info");
            }

            return mempoolSize;
        }

        protected async Task<string> UpdateBestHash()
        {
            string hash = String.Empty;
            try
            {
                ApiResponse response = await _apiRequester.GetRequestAsync(_endpoint, "/api/Consensus/getbestblockhash");
                hash = response.Content;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get best hash");
            }

            return hash;
        }

        protected async Task<(double, double)> UpdateWalletBalancec()
        {
            double confirmed = 0;
            double unconfirmed = 0;
            try
            {
                ApiResponse response = await _apiRequester.GetRequestAsync(_endpoint, "/api/FederationWallet/balance");
                confirmed = response.Content.balances[0].amountConfirmed / STRATOSHI;
                unconfirmed = response.Content.balances[0].amountUnconfirmed / STRATOSHI;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get wallet balance");
            }

            return (confirmed, unconfirmed);
        }

        protected async Task<Object> UpdateHistory()
        {
            object history = new Object();
            try
            {
                ApiResponse response = await _apiRequester.GetRequestAsync(_endpoint, "/api/FederationWallet/history", "maxEntriesToReturn=30");
                history = response.Content;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get history");
            }

            return history;
        }

        protected async Task<string> UpdateFedInfo()
        {
            string fedAddress = String.Empty;
            try
            {
                FedInfoResponse = await _apiRequester.GetRequestAsync(_endpoint, "/api/FederationGateway/info");
                fedAddress = FedInfoResponse.Content.multisigAddress;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to fed info");
            }

            return fedAddress;
        }

        protected async Task<List<PendingPoll>> UpdatePolls()
        {
            List<PendingPoll> polls = new List<PendingPoll>();
            try
            {
                ApiResponse response = await _apiRequester.GetRequestAsync(_endpoint, "/api/DefaultVoting/pendingpolls");
                List<PendingPoll> pendingPolls = JsonConvert.DeserializeObject<List<PendingPoll>>(response.Content.ToString());
                
                response = await _apiRequester.GetRequestAsync(_endpoint, "/api/DefaultVoting/finishedpolls");
                List<PendingPoll> approvedPolls = JsonConvert.DeserializeObject<List<PendingPoll>>(response.Content.ToString());

                return pendingPolls.Union(approvedPolls).OrderByDescending(p => p.Id).ToList();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update polls");
            }

            return polls;
        }
        
        protected async Task<int> UpdateFedMemberCount()
        {
            try
            {
                ApiResponse response = await _apiRequester.GetRequestAsync(_endpoint, "/api/DefaultVoting/fedmembers");
                var count = JsonConvert.DeserializeObject<List<string>>(response.Content.ToString());
                return count.Count;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update polls");
            }

            return 0;
        }

        Regex headerHeight = new Regex("Headers\\.Height:\\s+([0-9]+)", RegexOptions.Compiled);
        Regex walletHeight = new Regex("Wallet(\\[SC\\])*\\.Height:\\s+([0-9]+)", RegexOptions.Compiled);
        Regex orphanSize = new Regex("OrphanSize:\\s+([0-9]+)", RegexOptions.Compiled);
        Regex miningHistory = new Regex(@"at the timestamp he was supposed to\.[\r\n|\n|\r]+(.*)\.\.\.", RegexOptions.IgnoreCase);
        Regex asyncLoopStats = new Regex("====== Async loops ======   (.*)", RegexOptions.Compiled);
        Regex addressIndexer = new Regex("AddressIndexer\\.Height:\\s+([0-9]+)", RegexOptions.Compiled);
        protected async Task<NodeDashboardStats> UpdateDashboardStats()
        {
            var nodeDashboardStats = new NodeDashboardStats();
            try
            {
                string response;
                using (HttpClient client = new HttpClient())
                {
                    response = await client.GetStringAsync($"{_endpoint}/api/Dashboard/Stats").ConfigureAwait(false);
                    nodeDashboardStats.OrphanSize = orphanSize.Match(response).Groups[1].Value;
                    if (int.TryParse(headerHeight.Match(response).Groups[1].Value, out var headerHeightValue))
                    {
                        nodeDashboardStats.HeaderHeight = headerHeightValue;
                    }
                    if (int.TryParse(this.addressIndexer.Match(response).Groups[1].Value, out var height))
                    {
                        nodeDashboardStats.AddressIndexerHeight = height;
                    }

                    nodeDashboardStats.AsyncLoops = asyncLoopStats.Match(response).Groups[1].Value.Replace("[", "").Replace("]", "").Replace(" ", "").Replace("Running", "R").Replace("Faulted", ", F");
                    var hitOrMiss = miningHistory.Match(response).Groups[1].Value.Split("-".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    nodeDashboardStats.MissCount = Array.FindAll(hitOrMiss, x => x.Contains("MISS")).Length;

                    if (!string.IsNullOrEmpty(MiningPubKey))
                    {
                        nodeDashboardStats.LastMinedIndex = Array.IndexOf(hitOrMiss, $"[{MiningPubKey.Substring(0, 4)}]") + 1;
                        nodeDashboardStats.IsMining = 0 < nodeDashboardStats.LastMinedIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get /api/Dashboard/Stats");
            }

            return nodeDashboardStats;
        }
    }

    public class NodeGetDataServiceMainchainMiner : NodeGetDataService
    {
        public NodeGetDataServiceMainchainMiner(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory, string env) : base(apiRequester,
            endpoint, loggerFactory, env)
        {

        }
    }

    public class NodeGetDataServiceMultisig : NodeGetDataService
    {
        public (double confirmedBalance, double unconfirmedBalance) WalletBalance { get; set; } = (0, 0);
        public object WalletHistory { get; set; }
        public string FedAddress { get; set; }

        public NodeGetDataServiceMultisig(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory, string env) : base(apiRequester,
            endpoint, loggerFactory, env)
        {

        }

        public override async Task<NodeGetDataService> Update()
        {
            NodeDashboardStats = await UpdateDashboardStats();
            NodeStatus = await this.UpdateNodeStatus();
            LogRules = await this.UpdateLogRules();
            RawMempool = await this.UpdateMempool();
            BestHash = await this.UpdateBestHash();
            WalletBalance = await this.UpdateWalletBalancec();
            WalletHistory = await this.UpdateHistory();
            FedAddress = await this.UpdateFedInfo();
            return this;
        }
    }

    public class NodeDataServiceSidechainMultisig : NodeGetDataServiceMultisig
    {
        public NodeDataServiceSidechainMultisig(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory, string env) : base(apiRequester,
            endpoint, loggerFactory, env)
        {
        }

        public override async Task<NodeGetDataService> Update()
        {
            NodeDashboardStats = await UpdateDashboardStats();
            NodeStatus = await UpdateNodeStatus();
            LogRules = await UpdateLogRules();
            RawMempool = await UpdateMempool();
            BestHash = await UpdateBestHash();
            WalletBalance = await UpdateWalletBalancec();
            WalletHistory = await UpdateHistory();
            FedAddress = await UpdateFedInfo();
            PendingPolls = await UpdatePolls();
            FedMemberCount = await UpdateFedMemberCount();
            return this;
        }
    }

    public class NodeDataServicesSidechainMiner : NodeGetDataService
    {
        public NodeDataServicesSidechainMiner(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory, string env) : base(apiRequester,
            endpoint, loggerFactory, env)
        {
        }

        public override async Task<NodeGetDataService> Update()
        {
            NodeDashboardStats = await UpdateDashboardStats();
            NodeStatus = await UpdateNodeStatus();
            LogRules = await UpdateLogRules();
            RawMempool = await UpdateMempool();
            BestHash = await UpdateBestHash();
            PendingPolls = await UpdatePolls();
            FedMemberCount = await UpdateFedMemberCount();
            return this;
        }
    }
}

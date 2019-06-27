using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NUglify.Helpers;
using Stratis.FederatedSidechains.AdminDashboard.Entities;

namespace Stratis.FederatedSidechains.AdminDashboard.Services
{
    public abstract class NodeGetDataService
    {
        public NodeStatus NodeStatus { get; set; }
        public List<LogRule> LogRules { get; set; }
        public int RawMempool { get; set; } = 0;
        public string BestHash { get; set; } = String.Empty;
        //public (double confirmedBalance, double unconfirmedBalance) WalletBalance { get; set; } = (0, 0);
        //public Object WalletHistory { get; set; }
        public ApiResponse StatusResponse { get; set; }
        public ApiResponse FedInfoResponse { get; set; }
        public List<PendingPoll> PendingPolls { get; set; }

        protected const int STRATOSHI = 100_000_000;
        private ApiRequester _apiRequester;
        private string _endpoint;
        private readonly ILogger<NodeGetDataService> logger;

        public NodeGetDataService(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory)
        {
            _apiRequester = apiRequester;
            _endpoint = endpoint;
            this.logger = loggerFactory.CreateLogger<NodeGetDataService>();
        }

        public virtual async Task<NodeGetDataService> Update()
        {
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
                polls = JsonConvert.DeserializeObject<List<PendingPoll>>(response.Content.ToString());
            }
            catch (Exception ex)
            { 
                this.logger.LogError(ex, "Failed to update polls");
            }

            return polls;
        }
    }

    public class NodeGetDataServiceMainchainMiner : NodeGetDataService
    {
        public NodeGetDataServiceMainchainMiner(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory) : base(apiRequester,
            endpoint, loggerFactory)
        {

        }
    }

    public class NodeGetDataServiceMultisig : NodeGetDataService
    {
        public (double confirmedBalance, double unconfirmedBalance) WalletBalance { get; set; } = (0, 0);
        public object WalletHistory { get; set; }
        public string FedAddress { get; set; }

        public NodeGetDataServiceMultisig(ApiRequester apiRequester, string endpoint, ILoggerFactory logger) : base(apiRequester,
            endpoint, logger)
        {

        }

        public override async Task<NodeGetDataService> Update()
        {
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
        public NodeDataServiceSidechainMultisig(ApiRequester apiRequester, string endpoint, ILoggerFactory logger) : base(apiRequester,
            endpoint, logger)
        {
        }

        public override async Task<NodeGetDataService> Update()
        {
            NodeStatus = await UpdateNodeStatus();
            LogRules = await UpdateLogRules();
            RawMempool = await UpdateMempool();
            BestHash = await UpdateBestHash();
            WalletBalance = await UpdateWalletBalancec();
            WalletHistory = await UpdateHistory();
            FedAddress = await UpdateFedInfo();
            PendingPolls = await UpdatePolls();
            return this;
        }
    }

    public class NodeDataServicesSidechainMiner : NodeGetDataService
    {
        public NodeDataServicesSidechainMiner(ApiRequester apiRequester, string endpoint, ILoggerFactory loggerFactory) : base(apiRequester, endpoint, loggerFactory)
        {
        }

        public override async Task<NodeGetDataService> Update()
        {
            NodeStatus = await UpdateNodeStatus();
            LogRules = await UpdateLogRules();
            RawMempool = await UpdateMempool();
            BestHash = await UpdateBestHash();
            PendingPolls = await UpdatePolls();
            return this;
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using Newtonsoft.Json;
using Stratis.FederatedSidechains.AdminDashboard.Models;
using Microsoft.AspNetCore.SignalR;
using Stratis.FederatedSidechains.AdminDashboard.Hubs;
using Microsoft.Extensions.Caching.Distributed;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;

namespace Stratis.FederatedSidechains.AdminDashboard.Services
{
    public class FetchingBackgroundService : IHostedService, IDisposable
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;
        private readonly IDistributedCache distributedCache;
        public readonly IHubContext<DataUpdaterHub> updaterHub;
        private Timer dataRetrieverTimer;

        public FetchingBackgroundService(IDistributedCache distributedCache, IOptions<DefaultEndpointsSettings> defaultEndpointsSettings, IHubContext<DataUpdaterHub> hubContext)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
            this.distributedCache = distributedCache;
            this.updaterHub = hubContext;
        }

        /// <summary>
        /// Start the Fetching Background Service to Populate Dashboard Datas
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            DoWorkAsync(null);

            //TODO: Add timer setting in configuration file
            this.dataRetrieverTimer = new Timer(DoWorkAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            await Task.CompletedTask;
        }

        /// <summary>
        /// Retrieve all node information and store it in IDistributedCache object
        /// </summary>
        /// <returns></returns>
        private async Task BuildCacheAsync()
        {
            var walletName = "clintm";

            #region Stratis Node
            var stratisStatus = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            var stratisRawmempool = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Mempool/getrawmempool");
            var stratisBestBlock = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Consensus/getbestblockhash");
            var stratisWalletHistory = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, $"/api/Wallet/history?WalletName={walletName}&AccountName=account%200");
            var stratisWalletBalances = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, $"/api/Wallet/balance?WalletName={walletName}&AccountName=account%200");
            #endregion

            #region Sidechain Node
            var sidechainStatus = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Node/status");
            var sidechainRawmempool = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Mempool/getrawmempool");
            var sidechainBestBlock = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Consensus/getbestblockhash");
            var sidechainWalletHistory = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, $"/api/Wallet/history?WalletName={walletName}&AccountName=account%200");
            var sidechainWalletBalances = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, $"/api/FederationWallet/balance");
            #endregion

            var dashboardModel = new DashboardModel
            {
                Status = true,
                IsCacheBuilt = true,
                MainchainWalletAddress = "31EBX8oNk6GoPufm755yuFtbBgEPmjPvdK",
                SidechainWalletAddress = "pTEBX8oNk6GoPufm755yuFtbBgEPmjPvdK ",
                MiningPublicKeys = new string[] {"02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335", "02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335", "02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335"},
                StratisNode = new StratisNodeModel
                {
                    WebAPIUrl = string.Concat(this.defaultEndpointsSettings.StratisNode, "/api"),
                    SwaggerUrl = string.Concat(this.defaultEndpointsSettings.StratisNode, "/swagger"),
                    SyncingStatus = stratisStatus.Content.consensusHeight > 0 ? (stratisStatus.Content.blockStoreHeight / stratisStatus.Content.consensusHeight) * 100 : 0,
                    Peers = stratisStatus.Content.outboundPeers,
                    BlockHash = stratisBestBlock.Content,
                    BlockHeight = stratisStatus.Content.blockStoreHeight,
                    MempoolSize = stratisRawmempool.Content.Count,
                    FederationMembers = new object[] {},
                    History = stratisWalletHistory.Content.history[0].transactionsHistory,
                    ConfirmedBalance = (double)stratisWalletBalances.Content.balances[0].amountConfirmed / 100000000,
                    UnconfirmedBalance = (double)stratisWalletBalances.Content.balances[0].amountUnconfirmed / 100000000
                },  
                SidechainNode = new SidechainNodelModel
                {
                    WebAPIUrl = string.Concat(this.defaultEndpointsSettings.SidechainNode, "/api"),
                    SwaggerUrl = string.Concat(this.defaultEndpointsSettings.SidechainNode, "/swagger"),
                    SyncingStatus = sidechainStatus.Content.consensusHeight > 0 ? (sidechainStatus.Content.blockStoreHeight / sidechainStatus.Content.consensusHeight) * 100 : 0,
                    Peers = sidechainStatus.Content.outboundPeers,
                    BlockHash = sidechainBestBlock.Content,
                    BlockHeight = sidechainStatus.Content.blockStoreHeight,
                    MempoolSize = sidechainRawmempool.Content.Count,
                    FederationMembers = new object[] {},
                    History = new object[] {},
                    ConfirmedBalance = (double)sidechainWalletBalances.Content.balances[0].amountConfirmed / 100000000,
                    UnconfirmedBalance = (double)sidechainWalletBalances.Content.balances[0].amountUnconfirmed / 100000000
                }
            };
            
            if(!string.IsNullOrEmpty(this.distributedCache.GetString("DashboardData")))
            {
                if(JToken.DeepEquals(this.distributedCache.GetString("DashboardData"), JsonConvert.SerializeObject(dashboardModel)) == false)
                {
                    await this.updaterHub.Clients.All.SendAsync("CacheIsDifferent");
                }
            }
            this.distributedCache.SetString("DashboardData", JsonConvert.SerializeObject(dashboardModel));
        }

        private async void DoWorkAsync(object state)
        {
            if(this.PerformNodeCheck())
            {
                await this.BuildCacheAsync();
            }
            else
            {
                await this.distributedCache.SetStringAsync("NodeUnavailable", "true");
            }
        }
            
        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.dataRetrieverTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.dataRetrieverTimer?.Dispose();
        }

        /// <summary>
        /// Perform connection check with the nodes
        /// </summary>
        /// <remarks>The ports can be changed in the future</remarks>
        /// <returns>True if the connection are succeed</returns>
        private bool PerformNodeCheck() => this.PortCheck(37221) && this.PortCheck(38226);

        /// <summary>
        /// Perform a port TCP scan
        /// </summary>
        /// <param name="port">Specify the port to scan</param>
        /// <returns>True if the port is opened</returns>
        private bool PortCheck(int port)
        {
            using(TcpClient tcpClient = new TcpClient())
            {
                try
                {
                    tcpClient.Connect("127.0.0.1", port);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.FederatedSidechains.AdminDashboard.Entities;
using Stratis.FederatedSidechains.AdminDashboard.Helpers;
using Stratis.FederatedSidechains.AdminDashboard.Hubs;
using Stratis.FederatedSidechains.AdminDashboard.Models;
using Stratis.FederatedSidechains.AdminDashboard.Services;
using Stratis.FederatedSidechains.AdminDashboard.Settings;

namespace Stratis.FederatedSidechains.AdminDashboard.HostedServices
{
    public class FetchingBackgroundService : IHostedService, IDisposable
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;
        private readonly IDistributedCache distributedCache;
        private readonly IHubContext<DataUpdaterHub> updaterHub;
        private readonly ILogger<FetchingBackgroundService> logger;
        private bool successfullyBuilt;
        private Timer dataRetrieverTimer;

        public FetchingBackgroundService(IDistributedCache distributedCache, IOptions<DefaultEndpointsSettings> defaultEndpointsSettings, IHubContext<DataUpdaterHub> hubContext, ILogger<FetchingBackgroundService> logger)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
            this.distributedCache = distributedCache;
            this.updaterHub = hubContext;
            this.logger = logger;
        }

        /// <summary>
        /// Start the Fetching Background Service to Populate Dashboard Datas
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.LogInformation($"Starting the Fetching Background Service");

            DoWorkAsync(null);

            //TODO: Add timer setting in configuration file
            this.dataRetrieverTimer = new Timer(DoWorkAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            await Task.CompletedTask;
        }

        /// <summary>
        /// Retrieve all node information and store it in IDistributedCache object
        /// </summary>
        private async Task BuildCacheAsync()
        {
            this.logger.LogInformation($"Refresh the Dashboard Data");

            #region Stratis Node
            ApiResponse stratisStatus = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            ApiResponse stratisLogRules = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/logrules");
            ApiResponse stratisRawmempool = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Mempool/getrawmempool");
            ApiResponse stratisBestBlock = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Consensus/getbestblockhash");
            ApiResponse stratisWalletBalances = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/FederationWallet/balance");
            ApiResponse stratisWalletHistory = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/FederationWallet/history", "maxEntriesToReturn=30");
            ApiResponse stratisFederationInfo = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/FederationGateway/info");
            #endregion

            #region Sidechain Node
            ApiResponse sidechainStatus = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Node/status");
            ApiResponse sidechainLogRules = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/logrules");
            ApiResponse sidechainRawmempool = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Mempool/getrawmempool");
            ApiResponse sidechainBestBlock = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Consensus/getbestblockhash");
            ApiResponse sidechainWalletBalances = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/FederationWallet/balance");
            ApiResponse sidechainWalletHistory = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/FederationWallet/history", "maxEntriesToReturn=30");
            ApiResponse sidechainFederationInfo = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/FederationGateway/info");
            #endregion

            var stratisPeers = new List<Peer>();
            var stratisFederationMembers = new List<Peer>();
            var sidechainPeers = new List<Peer>();
            var sidechainFederationMembers = new List<Peer>();

            try
            {
                this.ParsePeers(stratisStatus, stratisFederationInfo, ref stratisPeers, ref stratisFederationMembers);
                this.ParsePeers(sidechainStatus, sidechainFederationInfo, ref sidechainPeers, ref sidechainFederationMembers);
            }
            catch(Exception e)
            {
                this.logger.LogError(e, "Unable to parse feeds");
            }

            var dashboardModel = new DashboardModel();
            try
            {
                dashboardModel = new DashboardModel
                {
                    Status = true,
                    IsCacheBuilt = true,
                    MainchainWalletAddress = stratisFederationInfo.Content.multisigAddress,
                    SidechainWalletAddress = sidechainFederationInfo.Content.multisigAddress,
                    MiningPublicKeys = stratisFederationInfo.Content.federationMultisigPubKeys,
                    StratisNode = new StratisNodeModel
                    {
                        WebAPIUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.StratisNode, "/api").ToString(),
                        SwaggerUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.StratisNode, "/swagger").ToString(),
                        SyncingStatus = stratisStatus.Content.consensusHeight > 0 ? (stratisStatus.Content.blockStoreHeight / stratisStatus.Content.consensusHeight) * 100 : 0,
                        Peers = stratisPeers,
                        FederationMembers = stratisFederationMembers,
                        BlockHash = stratisBestBlock.Content,
                        BlockHeight = stratisStatus.Content.blockStoreHeight,
                        MempoolSize = stratisRawmempool.Content.Count,
                        History = stratisWalletHistory.Content,
                        ConfirmedBalance = (double)stratisWalletBalances.Content.balances[0].amountConfirmed / 100000000,
                        UnconfirmedBalance = (double)stratisWalletBalances.Content.balances[0].amountUnconfirmed / 100000000,
                        CoinTicker = stratisStatus.Content.coinTicker ?? "STRAT",
                        LogRules = JsonConvert.DeserializeObject<List<LogRule>>(stratisLogRules.Content.ToString())
                    },
                    SidechainNode = new SidechainNodelModel
                    {
                        WebAPIUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.SidechainNode, "/api").ToString(),
                        SwaggerUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.SidechainNode, "/swagger").ToString(),
                        SyncingStatus = sidechainStatus.Content.consensusHeight > 0 ? (sidechainStatus.Content.blockStoreHeight / sidechainStatus.Content.consensusHeight) * 100 : 0,
                        Peers = sidechainPeers,
                        FederationMembers = sidechainFederationMembers,
                        BlockHash = sidechainBestBlock.Content,
                        BlockHeight = sidechainStatus.Content.blockStoreHeight,
                        MempoolSize = sidechainRawmempool.Content.Count,
                        History = sidechainWalletHistory.Content,
                        ConfirmedBalance = (double)sidechainWalletBalances.Content.balances[0].amountConfirmed / 100000000,
                        UnconfirmedBalance = (double)sidechainWalletBalances.Content.balances[0].amountUnconfirmed / 100000000,
                        CoinTicker = sidechainStatus.Content.coinTicker ?? "STRAT",
                        LogRules = JsonConvert.DeserializeObject<List<LogRule>>(sidechainLogRules.Content.ToString())
                    }
                };
            }
            catch(Exception e)
            {
                this.logger.LogError(e, "Unable to fetch feeds.");
            }

            if (!string.IsNullOrEmpty(this.distributedCache.GetString("DashboardData")))
            {
                if (JToken.DeepEquals(this.distributedCache.GetString("DashboardData"), JsonConvert.SerializeObject(dashboardModel)) == false)
                {
                    await this.updaterHub.Clients.All.SendAsync("CacheIsDifferent");
                }
            }
            this.distributedCache.SetString("DashboardData", JsonConvert.SerializeObject(dashboardModel));
        }

        private void ParsePeers(dynamic stratisStatus, dynamic federationInfo, ref List<Peer> peers, ref List<Peer> federationMembers)
        {
            foreach (dynamic peer in (JArray)stratisStatus.Content.outboundPeers)
            {
                var endpointRegex = new Regex("\\[([A-Za-z0-9:.]*)\\]:([0-9]*)");
                dynamic endpointMatches = endpointRegex.Matches(Convert.ToString(peer.remoteSocketEndpoint));
                var endpoint = new IPEndPoint(IPAddress.Parse(endpointMatches[0].Groups[1].Value), int.Parse(endpointMatches[0].Groups[2].Value));
                (Convert.ToString(federationInfo.Content.endpoints).Contains($"{endpoint.Address.MapToIPv4().ToString()}:{endpointMatches[0].Groups[2].Value}") ? federationMembers : peers)
                .Add(new Peer()
                {
                    Endpoint = peer.remoteSocketEndpoint,
                    Type = "outbound",
                    Height = peer.tipHeight,
                    Version = peer.version
                });
            }
            foreach (dynamic peer in (JArray)stratisStatus.Content.inboundPeers)
            {
                var endpointRegex = new Regex("\\[([A-Za-z0-9:.]*)\\]:([0-9]*)");
                dynamic endpointMatches = endpointRegex.Matches(Convert.ToString(peer.remoteSocketEndpoint));
                var endpoint = new IPEndPoint(IPAddress.Parse(endpointMatches[0].Groups[1].Value), int.Parse(endpointMatches[0].Groups[2].Value));
                (Convert.ToString(federationInfo.Content.endpoints).Contains($"{endpoint.Address.MapToIPv4().ToString()}:{endpointMatches[0].Groups[2].Value}") ? federationMembers : peers)
                .Add(new Peer()
                {
                    Endpoint = peer.remoteSocketEndpoint,
                    Type = "inbound",
                    Height = peer.tipHeight,
                    Version = peer.version
                });
            }
        }

        private async void DoWorkAsync(object state)
        {
            if (this.PerformNodeCheck())
            {
                await this.BuildCacheAsync();
                this.successfullyBuilt = true;
            }
            else
            {
                await this.distributedCache.SetStringAsync("NodeUnavailable", "true");
                if (this.successfullyBuilt)
                {
                    await this.updaterHub.Clients.All.SendAsync("NodeUnavailable");
                }
                await this.distributedCache.RemoveAsync("DashboardData");
                this.successfullyBuilt = false;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.LogInformation($"Stopping the Fetching Background Service");
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
        private bool PerformNodeCheck() => this.PortCheck(new Uri(this.defaultEndpointsSettings.StratisNode)) && this.PortCheck(new Uri(this.defaultEndpointsSettings.SidechainNode));

        /// <summary>
        /// Perform a TCP port scan
        /// </summary>
        /// <param name="port">Specify the port to scan</param>
        /// <returns>True if the port is opened</returns>
        private bool PortCheck(Uri endpointToCheck)
        {
            this.logger.LogInformation($"Perform a port check for {endpointToCheck.Host}:{endpointToCheck.Port}");
            using (var tcpClient = new TcpClient())
            {
                try
                {
                    this.logger.LogInformation($"Host {endpointToCheck.Host}:{endpointToCheck.Port} is available");
                    tcpClient.Connect(endpointToCheck.Host, endpointToCheck.Port);
                    return true;
                }
                catch
                {
                    this.logger.LogWarning($"Host {endpointToCheck.Host}:{endpointToCheck.Port} unavailable");
                    return false;
                }
            }
        }
    }
}
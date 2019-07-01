using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
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
    /// <summary>
    /// This Background Service fetch APIs an cache content
    /// </summary>
    public class FetchingBackgroundService : IHostedService, IDisposable
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;
        private readonly IDistributedCache distributedCache;
        private readonly IHubContext<DataUpdaterHub> updaterHub;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<FetchingBackgroundService> logger;
        private readonly ApiRequester apiRequester;
        private bool successfullyBuilt;
        private Timer dataRetrieverTimer;
        private readonly bool is50K = true;
        private NodeGetDataService nodeDataServiceMainchain;
        private NodeGetDataService nodeDataServiceSidechain;

        public FetchingBackgroundService(IDistributedCache distributedCache, DefaultEndpointsSettings defaultEndpointsSettings, IHubContext<DataUpdaterHub> hubContext, ILoggerFactory loggerFactory, ApiRequester apiRequester, IConfiguration configuration)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings;
            this.distributedCache = distributedCache;
            this.updaterHub = hubContext;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<FetchingBackgroundService>();
            this.apiRequester = apiRequester;
            if (this.defaultEndpointsSettings.SidechainNodeType == NodeTypes.TenK) this.is50K = false;

            this.logger.LogInformation("Default settings {settings}", defaultEndpointsSettings);
            if (this.is50K)
            {
                nodeDataServiceMainchain = new NodeGetDataServiceMultisig(this.apiRequester, this.defaultEndpointsSettings.StratisNode, this.loggerFactory, this.defaultEndpointsSettings.EnvType);
                nodeDataServiceSidechain = new NodeDataServiceSidechainMultisig(this.apiRequester, this.defaultEndpointsSettings.SidechainNode, this.loggerFactory, this.defaultEndpointsSettings.EnvType);
            }
            else
            {
                nodeDataServiceMainchain = new NodeGetDataServiceMainchainMiner(this.apiRequester, this.defaultEndpointsSettings.StratisNode, this.loggerFactory, this.defaultEndpointsSettings.EnvType);
                nodeDataServiceSidechain = new NodeDataServicesSidechainMiner(this.apiRequester, this.defaultEndpointsSettings.SidechainNode, this.loggerFactory, this.defaultEndpointsSettings.EnvType);
            }

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

            await nodeDataServiceMainchain.Update();
            await nodeDataServiceSidechain.Update();

            var stratisPeers = new List<Peer>();
            var stratisFederationMembers = new List<Peer>();
            var sidechainPeers = new List<Peer>();
            var sidechainFederationMembers = new List<Peer>();

            try
            {
                if (this.is50K)
                {
                    this.ParsePeers(nodeDataServiceMainchain, stratisPeers, stratisFederationMembers);
                    this.ParsePeers(nodeDataServiceSidechain, sidechainPeers, sidechainFederationMembers);
                }
                else
                {
                    this.ParsePeers(nodeDataServiceMainchain, stratisPeers);
                    this.ParsePeers(nodeDataServiceSidechain, sidechainPeers);
                }
            }
            catch(Exception e)
            {
                this.logger.LogError(e, "Unable to parse feeds");
            }

            var dashboardModel = new DashboardModel();
            try
            {
                dashboardModel.Status = true;
                dashboardModel.IsCacheBuilt = true;
                dashboardModel.MainchainWalletAddress = this.is50K ? ((NodeGetDataServiceMultisig)nodeDataServiceMainchain).FedAddress : string.Empty;
                dashboardModel.SidechainWalletAddress = this.is50K ? ((NodeDataServiceSidechainMultisig)nodeDataServiceSidechain).FedAddress : string.Empty;
                dashboardModel.MiningPublicKeys = nodeDataServiceMainchain.FedInfoResponse?.Content?.federationMultisigPubKeys ?? new JArray();

                var stratisNode = new StratisNodeModel();

                stratisNode.History = this.is50K ? ((NodeGetDataServiceMultisig)nodeDataServiceMainchain).WalletHistory : new JArray();
                stratisNode.ConfirmedBalance = this.is50K ? ((NodeGetDataServiceMultisig)nodeDataServiceMainchain).WalletBalance.confirmedBalance : -1;
                stratisNode.UnconfirmedBalance = this.is50K ? ((NodeGetDataServiceMultisig)nodeDataServiceMainchain).WalletBalance.unconfirmedBalance : -1;
                
                stratisNode.WebAPIUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.StratisNode, "/api").ToString();
                stratisNode.SwaggerUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.StratisNode, "/swagger").ToString();
                stratisNode.SyncingStatus = nodeDataServiceMainchain.NodeStatus.SyncingProgress;
                stratisNode.Peers = stratisPeers;
                stratisNode.FederationMembers = stratisFederationMembers;
                stratisNode.BlockHash = nodeDataServiceMainchain.BestHash;
                stratisNode.BlockHeight = (int)nodeDataServiceMainchain.NodeStatus.BlockStoreHeight;
                stratisNode.MempoolSize = nodeDataServiceMainchain.RawMempool;

                stratisNode.CoinTicker = "STRAT";
                stratisNode.LogRules = nodeDataServiceMainchain.LogRules;
                stratisNode.Uptime = nodeDataServiceMainchain.NodeStatus.Uptime;
                stratisNode.IsMining = this.nodeDataServiceMainchain.NodeDashboardStats?.IsMining ?? false;
                stratisNode.AddressIndexer = this.nodeDataServiceMainchain.NodeDashboardStats?.AddressIndexerHeight ?? 0;
                stratisNode.HeaderHeight = this.nodeDataServiceMainchain.NodeDashboardStats?.HeaderHeight ?? 0;
                stratisNode.AsyncLoops = this.nodeDataServiceMainchain.NodeDashboardStats?.AsyncLoops ?? string.Empty;
                stratisNode.OrphanSize = this.nodeDataServiceMainchain.NodeDashboardStats?.OrphanSize ?? string.Empty;

                dashboardModel.StratisNode = stratisNode;

                var sidechainNode = new SidechainNodeModel();

                sidechainNode.History = this.is50K ? ((NodeDataServiceSidechainMultisig)nodeDataServiceSidechain).WalletHistory : new JArray();
                sidechainNode.ConfirmedBalance = this.is50K ? ((NodeDataServiceSidechainMultisig)nodeDataServiceSidechain).WalletBalance.confirmedBalance : -1;
                sidechainNode.UnconfirmedBalance = this.is50K ? ((NodeDataServiceSidechainMultisig)nodeDataServiceSidechain).WalletBalance.unconfirmedBalance : -1;

                sidechainNode.WebAPIUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.SidechainNode, "/api").ToString();
                sidechainNode.SwaggerUrl = UriHelper.BuildUri(this.defaultEndpointsSettings.SidechainNode, "/swagger").ToString();
                sidechainNode.SyncingStatus = nodeDataServiceSidechain.NodeStatus.SyncingProgress;
                sidechainNode.Peers = sidechainPeers;
                sidechainNode.FederationMembers = sidechainFederationMembers;
                sidechainNode.BlockHash = nodeDataServiceSidechain.BestHash;
                sidechainNode.BlockHeight = (int) nodeDataServiceSidechain.NodeStatus.BlockStoreHeight;
                sidechainNode.MempoolSize = nodeDataServiceSidechain.RawMempool;

                sidechainNode.CoinTicker = "TCRS";
                sidechainNode.LogRules = nodeDataServiceSidechain.LogRules;
                sidechainNode.PoAPendingPolls = this.defaultEndpointsSettings.SidechainNodeType.ToUpper() == NodeTypes.FiftyK ? nodeDataServiceSidechain.PendingPolls : null;
                sidechainNode.Uptime = nodeDataServiceSidechain.NodeStatus.Uptime;
                sidechainNode.IsMining = this.nodeDataServiceSidechain.NodeDashboardStats?.IsMining ?? false;
                sidechainNode.AddressIndexer = this.nodeDataServiceSidechain.NodeDashboardStats?.AddressIndexerHeight ?? 0;
                sidechainNode.HeaderHeight = this.nodeDataServiceSidechain.NodeDashboardStats?.HeaderHeight ?? 0;
                sidechainNode.AsyncLoops = this.nodeDataServiceSidechain.NodeDashboardStats?.AsyncLoops ?? string.Empty;
                sidechainNode.OrphanSize = this.nodeDataServiceSidechain.NodeDashboardStats?.OrphanSize ?? string.Empty;

                dashboardModel.SidechainNode = sidechainNode;
            }
            catch(Exception e)
            {
                this.logger.LogError(e, "Unable to fetch feeds.");
                return;
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

        private void ParsePeers(NodeGetDataService dataService, List<Peer> peers, List<Peer> federationMembers)
        {
            string fedEndpoints = dataService.FedInfoResponse?.Content?.endpoints?.ToString() ?? string.Empty;

            if (dataService.StatusResponse.Content.outboundPeers is JArray outboundPeers)
            {
                this.LoadPeers(fedEndpoints, outboundPeers, "outbound", peers, federationMembers);
            }

            if (dataService.StatusResponse.Content.inboundPeers is JArray inboundPeers)
            {
                this.LoadPeers(fedEndpoints, inboundPeers, "inbound", peers, federationMembers);
            }
        }

        private void ParsePeers(NodeGetDataService dataService, List<Peer> peers)
        {
            if (dataService.StatusResponse.Content.outboundPeers is JArray outboundPeers)
            {
                this.LoadPeers(outboundPeers, "outbound", peers);
            }

            if (dataService.StatusResponse.Content.inboundPeers is JArray inboundPeers)
            {
                this.LoadPeers(inboundPeers, "inbound", peers);
            }
        }

        private void LoadPeers(JArray peersToProcess, string direction, List<Peer> peers)
        {
            foreach (dynamic peer in peersToProcess)
            {
                var peerToAdd = new Peer
                {
                    Endpoint = peer.remoteSocketEndpoint,
                    Type = direction,
                    Height = peer.tipHeight,
                    Version = peer.version
                };

                peers.Add(peerToAdd);
            }
        }

        private void LoadPeers(string fedEndpoints, JArray peersToProcess, string direction, List<Peer> peers, List<Peer> federationMembers)
        {
            foreach (dynamic peer in peersToProcess)
            {
                string peerIp = this.GetPeerIP(peer);
                var peerToAdd = new Peer
                {
                    Endpoint = peer.remoteSocketEndpoint,
                    Type = direction,
                    Height = peer.tipHeight,
                    Version = peer.version
                };

                if (fedEndpoints.Contains(peerIp))
                    federationMembers.Add(peerToAdd);
                else
                    peers.Add(peerToAdd);
            }
        }

        private string GetPeerIP(dynamic peer)
        {
            var endpointRegex = new Regex("\\[([A-Za-z0-9:.]*)\\]:([0-9]*)");
            MatchCollection endpointMatches = endpointRegex.Matches(Convert.ToString(peer.remoteSocketEndpoint));
            if (endpointMatches.Count <= 0 || endpointMatches[0].Groups.Count <= 1)
                return string.Empty;
            var endpoint = new IPEndPoint(IPAddress.Parse(endpointMatches[0].Groups[1].Value),
                int.Parse(endpointMatches[0].Groups[2].Value));

            return
                $"{endpoint.Address.MapToIPv4()}:{endpointMatches[0].Groups[2].Value}";
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

        /// <summary>
        /// When the service is disposed, the timer is disposed too
        /// </summary>
        public void Dispose()
        {
            this.dataRetrieverTimer?.Dispose();
        }

        /// <summary>
        /// Perform connection check with the nodes
        /// </summary>
        /// <remarks>The ports can be changed in the future</remarks>
        /// <returns>True if the connection are succeed</returns>
        private bool PerformNodeCheck()
        {
            var mainNodeUp = this.PortCheck(new Uri(this.defaultEndpointsSettings.StratisNode));
            var sidechainsNodeUp = this.PortCheck(new Uri(this.defaultEndpointsSettings.SidechainNode));
            return mainNodeUp && sidechainsNodeUp;
        } 

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
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Stratis.FederatedSidechains.AdminDashboard.Filters;
using Stratis.FederatedSidechains.AdminDashboard.Hubs;
using Stratis.FederatedSidechains.AdminDashboard.Models;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using Stratis.FederatedSidechains.AdminDashboard.Settings;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDistributedCache distributedCache;
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;
        private readonly IHubContext<DataUpdaterHub> updaterHub;

        public HomeController(IDistributedCache distributedCache, IHubContext<DataUpdaterHub> hubContext, IOptions<DefaultEndpointsSettings> defaultEndpointsSettings)
        {
            this.distributedCache = distributedCache;
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
            this.updaterHub = hubContext;
        }

        /// <summary>
        /// Check if the federation is enabled, it's only called from the SignalR event
        /// </summary>
        /// <returns>True or False</returns>
        [Ajax]
        [Route("check-federation")]
        public async Task<IActionResult> CheckFederationAsync()
        {
            ApiResponse getMainchainFederationInfo = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/FederationGateway/info");
            if(getMainchainFederationInfo.IsSuccess)
            {
                return Json(getMainchainFederationInfo.Content.active);
            }
            return Json(true);
        }

        /// <summary>
        /// This is the Index action that return the dashboard if the local cache is built otherwise the initialization page is displayed
        /// </summary>
        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(this.distributedCache.GetString("DashboardData")))
            {
                var nodeUnavailable = !string.IsNullOrEmpty(this.distributedCache.GetString("NodeUnavailable"));
                this.ViewBag.NodeUnavailable = nodeUnavailable;
                this.ViewBag.Status = nodeUnavailable ? "API Unavailable" : "Initialization...";
                return View("Initialization");
            }

            var dashboardModel = JsonConvert.DeserializeObject<DashboardModel>(this.distributedCache.GetString("DashboardData"));
            this.ViewBag.DisplayLoader = true;
            this.ViewBag.History = new[] {
                dashboardModel.StratisNode.History,
                dashboardModel.SidechainNode.History
            };
            this.ViewBag.StratisTicker = dashboardModel.StratisNode.CoinTicker;
            this.ViewBag.SidechainTicker= dashboardModel.SidechainNode.CoinTicker;
            this.ViewBag.MainchainMultisigAddress = dashboardModel.MainchainWalletAddress;
            this.ViewBag.SidechainMultisigAddress = dashboardModel.SidechainWalletAddress;
            this.ViewBag.MiningPubKeys = dashboardModel.MiningPublicKeys;
            this.ViewBag.Status = "OK";

            return View("Dashboard", dashboardModel);
        }

        /// <summary>
        /// This action redraw the dashboard with the new cached datas, it's only called from the SignalR event
        /// </summary>
        [Ajax]
        [Route("update-dashboard")]
        public IActionResult UpdateDashboard()
        {
            if(!string.IsNullOrEmpty(this.distributedCache.GetString("DashboardData")))
            {
                var dashboardModel = JsonConvert.DeserializeObject<DashboardModel>(this.distributedCache.GetString("DashboardData"));
                this.ViewBag.History = new[] {
                    dashboardModel.StratisNode.History,
                    dashboardModel.SidechainNode.History
                };
                this.ViewBag.StratisTicker = dashboardModel.StratisNode.CoinTicker;
                this.ViewBag.SidechainTicker= dashboardModel.SidechainNode.CoinTicker;
                return PartialView("Dashboard", dashboardModel);
            }
            return NoContent();
        }
    }
}

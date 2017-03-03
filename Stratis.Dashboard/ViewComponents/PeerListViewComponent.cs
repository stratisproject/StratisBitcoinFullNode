using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Dashboard.Infrastructure;
using Stratis.Dashboard.Model;

namespace Stratis.Dashboard.ViewComponents {
   public class PeerListViewComponent : ViewComponent {
      private IFullNodeGetter _nodeGetter;

      public PeerListViewComponent(IFullNodeGetter nodeGetter) {
         _nodeGetter = nodeGetter;
      }

      public async Task<IViewComponentResult> InvokeAsync(int? limit) {
         var fullNode = _nodeGetter.GetFullNode();

         var peers = (
            from node in fullNode.ConnectionManager.ConnectedNodes
            where node.IsConnected
            let peer = node.Peer
            select new PeerItem {
               ConnectedAt = node.ConnectedAt,
               Read = node.Counter.ReadenBytes,
               Written = node.Counter.WrittenBytes,
               EndPoint = peer.Endpoint,
               PeerUserAgent = node.PeerVersion.UserAgent,
               PeerVersion = node.PeerVersion.Version.ToString(),
               NegotiatedVersion = node.Version.ToString(),
            });

         return View(peers);
      }
   }
}
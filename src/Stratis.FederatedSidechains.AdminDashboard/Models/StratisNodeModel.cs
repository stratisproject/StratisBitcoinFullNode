using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Stratis.FederatedSidechains.AdminDashboard.Entities;

namespace Stratis.FederatedSidechains.AdminDashboard.Models
{
    public class StratisNodeModel
    {
        public float SyncingStatus { get; set; }
        public string WebAPIUrl { get; set; } = "http://localhost:38221/api";
        public string SwaggerUrl { get; set; } = "http://localhost:38221/swagger";
        public int BlockHeight { get; set; }
        public int MempoolSize { get; set; }
        public string BlockHash { get; set; }
        public double ConfirmedBalance { get; set; }
        public double UnconfirmedBalance { get; set; }
        public List<Peer> Peers { get; set; }
        public List<Peer> FederationMembers { get; set; }
        public object History { get; set; }
        public string CoinTicker { get; set; }
        public List<LogRule> LogRules { get; set; }
        public string OrphanSize { get; set; }
        public bool IsMining { get; set; }
        public string AsyncLoops { get; set; }
        public int HeaderHeight { get; set; }
        public int AddressIndexer { get; set; }

        public bool HasAsyncLoopsErrors
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.AsyncLoops)) return false;
                string[] tokens = this.AsyncLoops.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    return false;
                string failedCountToken = tokens.FirstOrDefault(t => t.Contains("F:", StringComparison.OrdinalIgnoreCase));
                if (failedCountToken == null)
                    return false;
                string[] keyValue = failedCountToken.Split(new[] {':'}, StringSplitOptions.RemoveEmptyEntries);
                if (keyValue.Length < 2)
                    return false;
                if (int.TryParse(keyValue[1], out var failedCount))
                {
                    return failedCount > 0;
                }

                return false;
            }
        }

        public string Uptime { get; set; }
    }
}
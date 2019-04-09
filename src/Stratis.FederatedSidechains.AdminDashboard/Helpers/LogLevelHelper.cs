using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.FederatedSidechains.AdminDashboard.Models;

namespace Stratis.FederatedSidechains.AdminDashboard.Helpers
{
    public static class LogLevelHelper
    {
        public static string[] DefaultLogRules
        {
            get
            {
                return new string[]
                {
                    "*",
                    "Stratis.Bitcoin.Features.Api.*",
                    "Stratis.Bitcoin.Features.BlockStore.*",
                    "Stratis.Bitcoin.Features.Consensus.*",
                    "Stratis.Bitcoin.Consensus.*",
                    "Stratis.Bitcoin.Consensus.ChainedHeaderTree",
                    "Stratis.Bitcoin.Consensus.ConsensusManager",
                    "Stratis.Bitcoin.Features.Consensus.CoinViews.*",
                    "Stratis.Bitcoin.Features.Dns.*",
                    "Stratis.Bitcoin.Features.LightWallet.*",
                    "Stratis.Bitcoin.Features.MemoryPool.*",
                    "Stratis.Bitcoin.Features.Miner.*",
                    "Stratis.Bitcoin.Features.Notifications.*",
                    "Stratis.Bitcoin.Features.RPC.*",
                    "Stratis.Bitcoin.Features.Wallet.*",
                    "Stratis.Bitcoin.Features.WatchOnlyWallet.*",
                    "Stratis.Bitcoin.Consensus.ConsensusManagerBehavior",
                    "Stratis.Bitcoin.Base.*",
                    "Stratis.Bitcoin.Base.TimeSyncBehaviorState",
                    "Stratis.Bitcoin.BlockPulling.*",
                    "Stratis.Bitcoin.Connection.*",
                    "Stratis.Bitcoin.P2P.*"
                };
            }
        }
    }
}

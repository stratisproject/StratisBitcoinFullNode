using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    using Wallet.Services;

    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class CirrusWalletInfoBroadcaster : WalletInfoBroadcaster
    {
        public CirrusWalletInfoBroadcaster(
            ILoggerFactory loggerFactory,
            IWalletService walletService,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            EventsHub eventsHub)
            : base(loggerFactory, walletService, asyncProvider, nodeLifetime, eventsHub, true)
        {
        }
    }
}
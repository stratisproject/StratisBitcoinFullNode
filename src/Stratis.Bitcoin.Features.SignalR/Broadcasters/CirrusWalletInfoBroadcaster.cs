using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet.Services;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    

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
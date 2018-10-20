//using System;
//using System.Collections.Concurrent;
//using System.Threading.Tasks;
//using City.Chain.Features.SimpleWallet.Hubs;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.DependencyInjection;
//using NBitcoin;

//namespace City.Chain.Features.SimpleWallet
//{
//    public class HubCommands
//    {
//        private IHubContext<SimpleWalletHub> hubContext;

//        private IServiceProvider serviceProvider;

//        public HubCommands(IServiceProvider serviceProvider)
//        {
//            this.serviceProvider = serviceProvider;
//            //this.hubContext = hubContext;
//        }

//        public Task SendTransactionsToUser(string connectionId, ConcurrentDictionary<uint256, Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData> txs)
//        {
//            if (this.hubContext == null)
//            {
//                this.hubContext = Startup.Provider.GetService<IHubContext<SimpleWalletHub>>();
//            }
            
//            return this.hubContext.Clients.Client(connectionId).SendAsync("txs", txs.ToArray());

//            //IHubContext<SimpleWalletHub> hubContext,
//            //return SendToUser(connectionId, "txs", txs);

//            //return this.hubContext.Clients.Client(connectionId).SendAsync("txs", txs);
//            //return _hubContext.Clients.All.SendAsync("AllocationCreated", allocation);
//        }

//        public Task SendTransactionsToUser2(string connectionId, ConcurrentDictionary<uint256, Transaction> txs)
//        {
//            if (this.hubContext == null)
//            {
//                this.hubContext = Startup.Provider.GetService<IHubContext<SimpleWalletHub>>();
//            }

//            return this.hubContext.Clients.Client(connectionId).SendAsync("txs", txs.ToArray());

//            //IHubContext<SimpleWalletHub> hubContext,
//            //return SendToUser(connectionId, "txs", txs);

//            //return this.hubContext.Clients.Client(connectionId).SendAsync("txs", txs);
//            //return _hubContext.Clients.All.SendAsync("AllocationCreated", allocation);
//        }

//        public Task SendToUser(string connectionId, string command, object arg1)
//        {
//            if (this.hubContext == null)
//            {
//                this.hubContext = Startup.Provider.GetService<IHubContext<SimpleWalletHub>>();
//            }

//            return this.hubContext.Clients.Client(connectionId).SendAsync(command, arg1);
//            //return _hubContext.Clients.All.SendAsync("AllocationCreated", allocation);
//        }
//    }
//}

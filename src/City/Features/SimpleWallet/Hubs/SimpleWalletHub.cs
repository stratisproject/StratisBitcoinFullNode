//using System;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.SignalR;
//using NBitcoin;

//namespace City.Chain.Features.SimpleWallet.Hubs
//{
//    /// <summary>
//    /// A transient wallet that only keeps wallets in-memory on the node.
//    /// </summary>
//    public class SimpleWalletHub : Hub
//    {
//        SimpleWalletService walletService;

//        public string WalletId { get { return this.Context.ConnectionId; } }

//        public SimpleWalletHub(SimpleWalletService walletService)
//        {
//            this.walletService = walletService;
//        }

//        /// <summary>
//        /// This is a non-persitent method to create an in-memory watch-only wallet. It is used to give the running node an in-memory-wallet to keep as a management
//        /// object to return events to the connect hub client.
//        /// </summary>
//        /// <param name="ver">The version header of the connecting client.</param>
//        /// <param name="created">The date the wallet started being used. If not supplied, it will use the current date (e.g. "new wallet") and not find old transactions.</param>
//        public void CreateWallet(string ver, DateTimeOffset? created, string[] addresses)
//        {
//            // TODO: Make it possible to configure the number of active wallets, to limit resource usage.
//            if (this.walletService.ActiveWalletCount() > 10)
//            {
//                throw new ApplicationException("Maximum number of active simple wallets created. Connect to another node.");
//            }
            
//            var walletManager = this.walletService.Create(this.WalletId, ver, created);

//            if (addresses != null)
//            {
//                foreach (var address in addresses)
//                {
//                    this.walletService.Watch(walletManager, address);
//                }
//            }

//            walletManager.Initialize();

//            // TODO: Remove soon as this will be done by the Feature.
//            //this.walletService.Initialize(this.WalletId); //  manager.Initialize();
//            //var hubContext = (IHubContext<MyHub>)context.RequestServices.GetServices<IHubContext<MyHub>>();
//        }

//        public Money Balance(string address)
//        {
//            if (string.IsNullOrWhiteSpace(address))
//            {
//                throw new ArgumentNullException("address");
//            }

//            return this.walletService.Balance(this.WalletId, address);
//        }

//        //public override Task OnConnectedAsync()
//        //{
//        //    return base.OnConnectedAsync();
//        //}

//        public override Task OnDisconnectedAsync(Exception exception)
//        {
//            this.Remove();
//            return base.OnDisconnectedAsync(exception);
//        }

//        /// <summary>
//        /// Adds an additional address to watch for. When new addresses are added, a full sync from the date provided in the Create is done. This can take some time.
//        /// </summary>
//        /// <param name="address"></param>
//        public void Watch(string address)
//        {
//            if (string.IsNullOrWhiteSpace(address))
//            {
//                throw new ArgumentNullException("address");
//            }
            
//            this.walletService.Watch(this.WalletId, address);
//        }

//        private void Remove()
//        {
//            var uniqueWalletId = this.Context.ConnectionId;
//            this.walletService.Remove(uniqueWalletId);
//        }
//    }
//}

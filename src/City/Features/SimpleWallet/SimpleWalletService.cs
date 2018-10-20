//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using NBitcoin;
//using Stratis.Bitcoin.Features.WatchOnlyWallet;

//namespace City.Chain.Features.SimpleWallet
//{
//    /// <summary>
//    /// Simple Wallet lookup-service that is registered in the IoC container for SignalR and retrieve on each request from the client. Holds all active
//    /// instances of SimpleWalletManager.
//    /// </summary>
//    public class SimpleWalletService
//    {
//        ConcurrentDictionary<string, SimpleWalletManager> walletManagers = new ConcurrentDictionary<string, SimpleWalletManager>();

//        /// <summary>Instance logger.</summary>
//        private readonly ILogger logger;

//        private readonly Network network;

//        private readonly IServiceProvider serviceProvider;

//        public SimpleWalletService(ILoggerFactory loggerFactory, ConcurrentChain chain, IServiceProvider serviceProvider)
//        {
//            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

//            this.serviceProvider = serviceProvider;

//            this.network = chain.Network;
//        }

//        public ISimpleWalletManager Create(string name, string version, DateTimeOffset? created)
//        {
//            // Create was called again on the same name? Reconnect to SimpleWallet, or some other reason? Log it, and delete the existing,
//            // then re-create. We don't want to leak existing wallets if a new user enters with a re-used identifier.
//            if (this.walletManagers.ContainsKey(name))
//            {
//                Remove(name);
//            }

//            // Create a new instance of the SimpleWalletManager using IoC container to fullfill the dependencies.
//            SimpleWalletManager walletManager = this.serviceProvider.GetService<SimpleWalletManager>();

//            // Configure the wallet manager.
//            walletManager.Configure(name, version, created);

//            // TODO: Handle failures.
//            this.walletManagers.TryAdd(name, walletManager);

//            return walletManager;
//        }

//        private SimpleWalletManager GetWalletManager(string name)
//        {
//            if (!this.walletManagers.ContainsKey(name))
//            {
//                throw new ApplicationException("The wallet does not exists. Make sure you call Create before Watch.");
//            }

//            return this.walletManagers.TryGet(name);
//        }

//        internal void Initialize(string walletId)
//        {
            
//        }

//        /// <summary>
//        /// Returns the current number of active simple wallets. This value is used to limit amount of active connections to the hub.
//        /// </summary>
//        /// <returns></returns>
//        public int ActiveWalletCount()
//        {
//            return this.walletManagers.Count;
//        }

//        public void Watch(string name, string address)
//        {
//            var manager = GetWalletManager(name);
//            Watch(manager, address);
//        }

//        public void Watch(ISimpleWalletManager manager, string address)
//        {
//            var wallet = manager.GetWatchOnlyWallet();

//            Script script = BitcoinAddress.Create(address, this.network).ScriptPubKey;

//            if (wallet.WatchedAddresses.ContainsKey(script.ToString()))
//            {
//                //this.logger.LogDebug($"already watching script: {script}. coin: {this.coinType}");
//                return;
//            }

//            //this.logger.LogDebug($"added script: {script} to the watch list. coin: {this.coinType}");

//            wallet.WatchedAddresses.TryAdd(script.ToString(), new WatchedAddress
//            {
//                Script = script,
//                Address = address
//            });
//        }

//        public Money Balance(string name, string address)
//        {
//            var manager = GetWalletManager(name);

//            var balance = manager.GetRelativeBalance(address);

//            return balance;
//        }

//        public void Remove(string name)
//        {
//            if (this.walletManagers.ContainsKey(name))
//            {
//                // Get the wallet manager.
//                var walletManager = this.walletManagers[name];

//                SimpleWalletManager tmp;

//                // Remove it from the dictionary.
//                // TODO: Handle failures.
//                var success = this.walletManagers.TryRemove(name, out tmp);

//                // Dispose the wallet manager.
//                walletManager.Dispose();

//                walletManager = null;
//            }
//        }

//        public void ProcessBlock(Block block)
//        {
//            foreach (KeyValuePair<string, SimpleWalletManager> walletManager in this.walletManagers)
//            {
//                walletManager.Value.ProcessAndNotify(null, block);
//            }
//        }

//        public void ProcessTransaction(Transaction transaction, Block block = null)
//        {
//            foreach (KeyValuePair<string, SimpleWalletManager> walletManager in this.walletManagers)
//            {
//                walletManager.Value.ProcessAndNotify(transaction, block);
//            }
//        }
//    }
//}

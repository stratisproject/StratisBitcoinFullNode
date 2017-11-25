using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeContext : IDisposable
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        private List<IDisposable> cleanList = new List<IDisposable>();
        private TestDirectory testDirectory;

        public NodeContext(string name, Network network, bool clean)
        {
            network = network ?? Network.RegTest;
            this.loggerFactory = new LoggerFactory();
            this.Network = network;
            this.testDirectory = TestDirectory.Create(name, clean);
            this.PersistentCoinView = new DBreezeCoinView(network, this.testDirectory.FolderName, DateTimeProvider.Default, this.loggerFactory);
            this.PersistentCoinView.InitializeAsync().GetAwaiter().GetResult();
            this.cleanList.Add(this.PersistentCoinView);
        }

        public Network Network { get; }

        private ChainBuilder chainBuilder;

        public ChainBuilder ChainBuilder
        {
            get
            {
                return this.chainBuilder = this.chainBuilder ?? new ChainBuilder(this.Network);
            }
        }

        public DBreezeCoinView PersistentCoinView { get; private set; }

        public string FolderName
        {
            get
            {
                return this.testDirectory.FolderName;
            }
        }

        public static NodeContext Create([CallerMemberName]string name = null, Network network = null, bool clean = true)
        {
            return new NodeContext(name, network, clean);
        }

        public void Dispose()
        {
            foreach (var item in this.cleanList)
                item.Dispose();
            this.testDirectory.Dispose(); //Not into cleanlist because it must run last
        }

        public void ReloadPersistentCoinView()
        {
            this.PersistentCoinView.Dispose();
            this.cleanList.Remove(this.PersistentCoinView);
            this.PersistentCoinView = new DBreezeCoinView(this.Network, this.testDirectory.FolderName, DateTimeProvider.Default, this.loggerFactory);
            this.PersistentCoinView.InitializeAsync().GetAwaiter().GetResult();
            this.cleanList.Add(this.PersistentCoinView);
        }
    }
}

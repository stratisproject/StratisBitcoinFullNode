using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeContext : IDisposable
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        private readonly List<IDisposable> cleanList;

        public NodeContext(object caller, string name, Network network, bool clean)
        {
            network = network ?? KnownNetworks.RegTest;
            this.loggerFactory = new LoggerFactory();
            this.Network = network;
            this.FolderName = TestBase.CreateTestDir(caller, name);
            var dateTimeProvider = new DateTimeProvider();
            var serializer = new DBreezeSerializer(this.Network);
            this.PersistentCoinView = new DBreezeCoinView(network, this.FolderName, dateTimeProvider, this.loggerFactory, new NodeStats(dateTimeProvider), serializer);
            this.PersistentCoinView.InitializeAsync().GetAwaiter().GetResult();
            this.cleanList = new List<IDisposable> {this.PersistentCoinView};
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

        public string FolderName { get; }

        public static NodeContext Create(object caller, [CallerMemberName]string name = null, Network network = null, bool clean = true)
        {
            return new NodeContext(caller, name, network, clean);
        }

        public void Dispose()
        {
            foreach (IDisposable item in this.cleanList)
                item.Dispose();
        }

        public void ReloadPersistentCoinView()
        {
            this.PersistentCoinView.Dispose();
            this.cleanList.Remove(this.PersistentCoinView);
            var dateTimeProvider = new DateTimeProvider();
            var serializer = new DBreezeSerializer(this.Network);
            this.PersistentCoinView = new DBreezeCoinView(this.Network, this.FolderName, dateTimeProvider, this.loggerFactory, new NodeStats(dateTimeProvider), serializer);
            this.PersistentCoinView.InitializeAsync().GetAwaiter().GetResult();
            this.cleanList.Add(this.PersistentCoinView);
        }
    }
}

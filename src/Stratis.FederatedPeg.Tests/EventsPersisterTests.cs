using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class EventsPersisterTests : IDisposable
    {
        private readonly Mock<ICrossChainTransferStore> mockStore;

        private readonly ICrossChainTransferStore store;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly IMaturedBlocksRequester maturedBlocksRequester;

        private readonly ILoggerFactory loggerFactory;

        private EventsPersister eventsPersister;

        public EventsPersisterTests()
        {
            this.mockStore = new Mock<ICrossChainTransferStore>();
            this.store = this.mockStore.Object;
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.maturedBlockReceiver = Substitute.For<IMaturedBlockReceiver>();
            this.maturedBlocksRequester = Substitute.For<IMaturedBlocksRequester>();
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_Upon_New_Block_Arrival()
        {
            int depositCount = 20;
            var deposits = new List<IMaturedBlockDeposits[]>();
            deposits.Add(new[] { new MaturedBlockDepositsModel(new MaturedBlockModel()
            {
                BlockHash = 0,
                BlockHeight = 0
            }, TestingValues.GetMaturedBlockDeposits(depositCount, new HashHeightPair(0, 0)).Deposits)});

            IObservable<IMaturedBlockDeposits[]> maturedBlockStream = deposits.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver, this.maturedBlocksRequester);

            var indexedCallArguments = this.mockStore.Invocations
                .Where(i => i.Method.Name == nameof(ICrossChainTransferStore.RecordLatestMatureDepositsAsync))
                .Select((c, i) => new { Index = i, Deposits = ((IMaturedBlockDeposits[])c.Arguments[0])[0].Deposits }).ToList();

            indexedCallArguments.ForEach(
                ca =>
                {
                    ca.Deposits.Count.Should().Be(depositCount);
                    ca.Deposits.Select(d => d).Should().BeEquivalentTo(deposits[ca.Index][0].Deposits);
                });
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_Upon_New_Block_Arrival_And_No_Deposits_In_Block()
        {
            int depositCount = 0;
            var deposits = new List<IMaturedBlockDeposits[]>();
            deposits.Add(new[] { new MaturedBlockDepositsModel(new MaturedBlockModel()
            {
                BlockHash = 0,
                BlockHeight = 0
            }, TestingValues.GetMaturedBlockDeposits(depositCount, new HashHeightPair(0, 0)).Deposits)});

            IObservable<IMaturedBlockDeposits[]> maturedBlockStream = deposits.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver, this.maturedBlocksRequester);

            var indexedCallArguments = this.mockStore.Invocations
                .Where(i => i.Method.Name == nameof(ICrossChainTransferStore.RecordLatestMatureDepositsAsync))
                .Select((c, i) => new { Index = i, Deposits = ((IMaturedBlockDeposits[])c.Arguments[0])[0].Deposits }).ToList();

            indexedCallArguments.ForEach(
                ca =>
                {
                    ca.Deposits.Count.Should().Be(depositCount);
                    ca.Deposits.Select(d => d).Should().BeEquivalentTo(deposits[ca.Index][0].Deposits);
                });
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_For_Each_Incomming_Matured_Block()
        {
            int blocksCount = 10;
            var deposits = new List<IMaturedBlockDeposits[]>();
            for (int i = 0; i < blocksCount; i++)
                deposits.Add(new[] { new MaturedBlockDepositsModel(new MaturedBlockModel()
                {
                    BlockHash = new uint256((ulong)i),
                    BlockHeight = i
                }, TestingValues.GetMaturedBlockDeposits(i, new HashHeightPair((uint)i, i)).Deposits)});

            IObservable<IMaturedBlockDeposits[]> maturedBlockStream = deposits.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            int blockNum = 0;
            this.mockStore.SetupGet(o => o.NextMatureDepositHeight).Returns(() => blockNum);
            this.mockStore.Setup(mock => mock.RecordLatestMatureDepositsAsync(It.IsAny<IMaturedBlockDeposits[]>()))
                .Returns(() => Task<bool>.Run(() => { blockNum++; return true; }));

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver, this.maturedBlocksRequester);

            var indexedCallArguments = this.mockStore.Invocations
                .Where(i => i.Method.Name == nameof(ICrossChainTransferStore.RecordLatestMatureDepositsAsync))
                .Select((c, i) => new { Index = i, Deposits = ((IMaturedBlockDeposits[])c.Arguments[0])[0].Deposits }).ToList();

            indexedCallArguments.ForEach(
                ca =>
                    {
                        ca.Deposits.Count.Should().Be(ca.Index);
                        ca.Deposits.Select(d => d).Should().BeEquivalentTo(deposits[ca.Index][0].Deposits);
                    });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.eventsPersister?.Dispose();
        }
    }
}

using System;
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
            var maturedBlockDeposits = TestingValues.GetMaturedBlockDeposits(depositCount, new HashHeightPair(0, 0));
            IObservable<IMaturedBlockDeposits[]> maturedBlockStream = new[] { new[] { maturedBlockDeposits } }.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver, this.maturedBlocksRequester);

            var indexedCallArguments = this.mockStore.Invocations
                .Where(i => i.Method.Name == nameof(ICrossChainTransferStore.RecordLatestMatureDepositsAsync))
                .Select((c, i) => new { Index = i, Deposits = (IDeposit[])c.Arguments[0] }).ToList();

            indexedCallArguments.ForEach(
                ca =>
                {
                    ca.Deposits.Length.Should().Be(depositCount);
                    ca.Deposits.Select(d => d).Should().BeEquivalentTo(maturedBlockDeposits.Deposits);
                });
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_Upon_New_Block_Arrival_And_No_Deposits_In_Block()
        {
            int depositCount = 0;
            var maturedBlockDeposits = TestingValues.GetMaturedBlockDeposits(depositCount, new HashHeightPair(0, 0));
            IObservable<IMaturedBlockDeposits[]> maturedBlockStream = new[] { new[] { maturedBlockDeposits } }.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver, this.maturedBlocksRequester);

            var indexedCallArguments = this.mockStore.Invocations
                .Where(i => i.Method.Name == nameof(ICrossChainTransferStore.RecordLatestMatureDepositsAsync))
                .Select((c, i) => new { Index = i, Deposits = (IDeposit[])c.Arguments[0] }).ToList();

            indexedCallArguments.ForEach(
                ca =>
                {
                    ca.Deposits.Length.Should().Be(depositCount);
                });
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_For_Each_Incomming_Matured_Block()
        {
            int blocksCount = 10;
            var maturedBlockDepositsEnum = Enumerable.Range(0,blocksCount)
                .Select(i => TestingValues.GetMaturedBlockDeposits(i, new HashHeightPair((uint256)(uint)i, i)))
                .ToList();

            IObservable<IMaturedBlockDeposits[]> maturedBlockStream = (new[] { maturedBlockDepositsEnum.ToArray() }).ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            int blockNum = 0;
            this.mockStore.SetupGet(o => o.NextMatureDepositHeight).Returns(() => blockNum);
            this.mockStore.Setup(mock => mock.RecordLatestMatureDepositsAsync(It.IsAny<IDeposit[]>()))
                .Returns(() => Task.Run(() => { blockNum++; }));

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver, this.maturedBlocksRequester);

            var indexedCallArguments = this.mockStore.Invocations
                .Where(i => i.Method.Name == nameof(ICrossChainTransferStore.RecordLatestMatureDepositsAsync))
                .Select((c, i) => new { Index = i, Deposits = (IDeposit[])c.Arguments[0] }).ToList();

            indexedCallArguments.ForEach(
                ca =>
                    {
                        ca.Deposits.Length.Should().Be(ca.Index);
                        ca.Deposits.Select(d => d).Should().BeEquivalentTo(maturedBlockDepositsEnum[ca.Index].Deposits);
                    });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.eventsPersister?.Dispose();
        }
    }
}

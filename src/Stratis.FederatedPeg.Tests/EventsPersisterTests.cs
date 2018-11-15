using System;
using System.Linq;
using System.Reactive.Linq;

using NSubstitute;
using System.Threading.Tasks;

using DBreeze.Utils;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using NSubstitute.Core;

using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;


namespace Stratis.FederatedPeg.Tests
{
    public class EventsPersisterTests : IDisposable
    {
        private readonly ICrossChainTransferStore store;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILoggerFactory loggerFactory;

        private EventsPersister eventsPersister;

        public EventsPersisterTests()
        {
            this.store = Substitute.For<ICrossChainTransferStore>();
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.maturedBlockReceiver = Substitute.For<IMaturedBlockReceiver>();
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_Upon_New_Block_Arrival()
        {
            int depositCount = 20;
            var maturedBlockDeposits = TestingValues.GetMaturedBlockDeposit(depositCount);
            IObservable<IMaturedBlockDeposits> maturedBlockStream = new[] { maturedBlockDeposits }.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver);
            
            this.store.Received(1).RecordLatestMatureDepositsAsync(Arg.Is<IDeposit[]>(
                deposits => deposits.Length == depositCount 
                            && deposits.All(d => maturedBlockDeposits.Deposits.Contains(d))));
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_Upon_New_Block_Arrival_And_No_Deposits_In_Block()
        {
            int depositCount = 0;
            var maturedBlockDeposits = TestingValues.GetMaturedBlockDeposit(depositCount);
            IObservable<IMaturedBlockDeposits> maturedBlockStream = new[] { maturedBlockDeposits }.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver);

            this.store.Received(1).RecordLatestMatureDepositsAsync(Arg.Is<IDeposit[]>(
                deposits => deposits.Length == depositCount));
        }

        [Fact]
        public void PersistNewMaturedBlockDeposits_Should_Call_Store_And_Pass_Deposits_For_Each_Incomming_Matured_Block()
        {
            int blocksCount = 10;
            var maturedBlockDepositsEnum = Enumerable.Range(0,blocksCount)
                .Select(TestingValues.GetMaturedBlockDeposit)
                .ToList();

            IObservable<IMaturedBlockDeposits> maturedBlockStream = maturedBlockDepositsEnum.ToObservable();
            this.maturedBlockReceiver.MaturedBlockDepositStream.Returns(maturedBlockStream);

            this.eventsPersister = new EventsPersister(this.loggerFactory, this.store, this.maturedBlockReceiver);

            var storeCalls = this.store.ReceivedCalls();
            var indexedCallArguments = storeCalls.Select((c, i) => new { Index = i, Deposits = (IDeposit[])c.GetArguments()[0]}).ToList();

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

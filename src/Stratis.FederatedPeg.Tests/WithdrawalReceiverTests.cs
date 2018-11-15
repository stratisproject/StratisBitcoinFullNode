using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FluentAssertions;

using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;

using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class WithdrawalReceiverTests : IDisposable
    {
        private IWithdrawalReceiver withdrawalReceiver;

        private IDisposable streamSubscription;

        [Fact]
        public void ReceiveWithdrawals_Should_Push_An_Item_In_NewWithdrawalsOnTargetChainStream()
        {
            this.withdrawalReceiver = new WithdrawalReceiver();

            var receivedWithdrawalListCount = 0;
            this.streamSubscription = this.withdrawalReceiver.NewWithdrawalsOnTargetChainStream.Subscribe(
                _ => { Interlocked.Increment(ref receivedWithdrawalListCount); });

            var withdrawals = Enumerable.Range(0, 10).Select(i => TestingValues.GetWithdrawal()).ToList().AsReadOnly();

            this.withdrawalReceiver.ReceiveWithdrawals(withdrawals);

            receivedWithdrawalListCount.Should().Be(1);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.streamSubscription?.Dispose();
            this.withdrawalReceiver?.Dispose();
        }
    }
}

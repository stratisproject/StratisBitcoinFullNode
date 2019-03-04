using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class WithdrawalReceiverTests : IDisposable
    {
        private WithdrawalReceiver withdrawalReceiver;


        private IDisposable streamSubscription;

        [Fact(Skip = TestingValues.SkipTests)]
        public void ReceiveWithdrawals_Should_Push_An_Item_In_NewWithdrawalsOnTargetChainStream()
        {
            this.withdrawalReceiver = new WithdrawalReceiver();

            int receivedWithdrawalListCount = 0;
            this.streamSubscription = this.withdrawalReceiver.NewWithdrawalsOnTargetChainStream.Subscribe(
                _ => { Interlocked.Increment(ref receivedWithdrawalListCount); });

            IReadOnlyList<IWithdrawal> withdrawals = TestingValues.GetWithdrawals(3);

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

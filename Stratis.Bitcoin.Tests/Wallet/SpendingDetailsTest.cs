using Stratis.Bitcoin.Wallet;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.Wallet
{
    public class SpendingDetailsTest
    {
        [Fact]
        public void IsSpentConfirmedHavingBlockHeightReturnsTrue()
        {
            var spendingDetails = new SpendingDetails()
            {
                BlockHeight = 15
            };

            Assert.True(spendingDetails.IsSpentConfirmed());
        }

        [Fact]
        public void IsConfirmedHavingNoBlockHeightReturnsFalse()
        {
            var spendingDetails = new SpendingDetails()
            {
                BlockHeight = null
            };

            Assert.False(spendingDetails.IsSpentConfirmed());
        }
    }
}

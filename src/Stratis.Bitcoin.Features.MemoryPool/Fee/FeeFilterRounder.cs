using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using System.Linq;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    public class FeeFilterRounder
    {
        private const decimal MaxFilterFeeRate = 1e7M;
        private const decimal FeeFilterSpacing = 1.1M;

        private SortedSet<decimal> feeSet;

        public FeeFilterRounder(FeeRate minIncrementalFee)
        {
            Money minFeeLimit = Math.Max(new Money(1), minIncrementalFee.FeePerK / 2);
            this.feeSet = new SortedSet<decimal>();
            this.feeSet.Add(0);
            for (decimal bucketBoundary = minFeeLimit.ToDecimal(MoneyUnit.BTC); bucketBoundary <= MaxFilterFeeRate; bucketBoundary *= FeeFilterSpacing)
            {
                this.feeSet.Add(bucketBoundary);
            }
        }

        public Money Round(Money currentMinFee)
        {
            var it = this.feeSet.FirstOrDefault(f => f >= currentMinFee.ToDecimal(MoneyUnit.BTC));
            if ((it != this.feeSet.FirstOrDefault() && RandomUtils.GetInt32() % 3 != 0) || it == this.feeSet.LastOrDefault())
            {
                it--;
            }
            return new Money(it, MoneyUnit.BTC);
        }
    }
}

using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    public class StratisBlockPolicyEstimator : IBlockPolicyEstimator
    {
        // TODO: Need to complete the Bitcoin version of the algorithm, then restructure it to compute its constants in a non-network-specific way

        public FeeRate EstimateFee(int confTarget)
        {
            throw new System.NotImplementedException();
        }

        public bool RemoveTx(uint256 hash, bool inBlock)
        {
            throw new System.NotImplementedException();
        }

        public FeeRate EstimateRawFee(int confTarget, double successThreshold, FeeEstimateHorizon horizon, EstimationResult result)
        {
            throw new System.NotImplementedException();
        }

        public FeeRate EstimateSmartFee(int confTarget, FeeCalculation feeCalc, bool conservative)
        {
            throw new System.NotImplementedException();
        }

        public void ProcessBlock(int nBlockHeight, List<TxMempoolEntry> entries)
        {
            throw new System.NotImplementedException();
        }

        public void ProcessTransaction(TxMempoolEntry entry, bool validFeeEstimate)
        {
            throw new System.NotImplementedException();
        }

        public int HighestTargetTracked(FeeEstimateHorizon horizon)
        {
            throw new System.NotImplementedException();
        }

        public void Write()
        {
            throw new System.NotImplementedException();
        }

        public bool Read()
        {
            throw new System.NotImplementedException();
        }

        public void FlushUnconfirmed()
        {
            throw new System.NotImplementedException();
        }
    }
}

using Xunit;

namespace Stratis.Bitcoin.Tests.Features.MemoryPool
{
    public class MempoolValidatorTest
    {
        #region CheckFinalTransaction

        [Fact]
        public void CheckFinalTransaction_WithStandardLockTimeAndValidTxTime_ReturnsTrue()
        {
            //TODO: Implement test
        }

        [Fact]
        public void CheckFinalTransaction_WithNoLockTimeAndValidTxTime_ReturnsTrue()
        {
            // TODO: Implement test
        }

        [Fact]
        public void CheckFinalTransaction_WithStandardLockTimeAndExpiredTxTime_Fails()
        {
            // TODO: Implement test
        }

        [Fact]
        public void CheckFinalTransaction_WithNoLockTimeLockTimeAndExpiredTxTime_Fails()
        {
            // TODO: Implement test
        }

        #endregion

        #region CheckSequenceLocks

        [Fact]
        public void CheckSequenceLocks_WithExistingLockPointAndValidChain_ReturnsTrue()
        {
            // TODO: Test case- Check two cases, chain with/without previous block
            // No Previous - time of lock == 0
            // Previous - time of lock < tip chain median time
        }

        [Fact]
        public void CheckSequenceLocks_WithExistingLockPointAndChainWithPrevAndExpiredBlockTime_Fails()
        {
            // TODO: Test case - The lock point MinTime exceeds chain previous block median time
        }

        [Fact]
        public void CheckSequenceLocks_WithExistingLockPointAndChainWihtNoPrevAndExpiredBlockTime_Fails()
        {
            // TODO: Test case - the lock point MinTime exceeds 0
        }


        [Fact]
        public void CheckSequenceLocks_WithExistingLockPointAndBadHeight_Fails()
        {
            //TODO: Test case - lock point height exceeds chain height
        }

        #endregion

        #region GetTransactionWeight

        [Fact]
        void GetTransactionWeight_WitnessTx_ReturnsWeight()
        {
            // TODO: Test getting tx weight on transaction with witness

        }

        [Fact]
        void GetTransactionWeight_StandardTx_ReturnsWeight()
        {
            // TODO: Test getting tx weight on transaction without witness
        }

        #endregion

        #region CalculateModifiedSize

        [Fact]
        public void CalculateModifiedSize_CalcWeightWithTxIns_ReturnsSize()
        {
            // TODO: Test GetTransactionWeight - InputSizes = CalculateModifiedSize
            // Test weight calculation by input nTxSize = 0
        }

        [Fact]
        public void CalculateModifiedSize_PreCalcWeightWithTxIns_ReturnsSize()
        {
            // TODO: Test GetTransactionWeight - InputSizes = CalculateModifiedSize
            // Test weight calculation by passing in weight as nTxSize, nTxSize can be computed with GetTransactionWeight()
        }

        #endregion

        #region AcceptToMemoryPool

        [Fact]
        public void AcceptToMemoryPool_TxIsCoinbase_ReturnsFalse()
        {
            // TODO: Execute this case PreMempoolChecks context.Transaction.IsCoinBase
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandard_ReturnsFalse()
        {
            // TODO:Test the caeses in PreMempoolChecks CheckStandardTransaction
            // - Check version number
            // - Check transaction size
            // - Check input scriptsig lengths
            // - Check input scriptsig push only
            // - Check output scripttemplate == null
            // - Check output dust
            // - Checkout output single return
        }

        [Fact]
        public void AcceptToMemoryPool_TxFinalCannotMine_ReturnsFalse()
        {
            // TODO: Execute cases in PreMempoolChecks CheckFinalTransaction
        }

        [Fact]
        public void AcceptToMemoryPool_TxAlreadyExists_ReturnsFalse()
        {
            // TODO: Execute this case this.memPool.Exists(context.TransactionHash)
        }

        [Fact]
        public void AcceptToMemoryPool_TxAlreadyHaveCoins_ReturnsFalse()
        {
            // TODO: Execute this case CheckMempoolCoinView context.View.HaveCoins(context.TransactionHash)
        }

        [Fact]
        public void AcceptToMemoryPool_TxMissingInputs_ReturnsFalse()
        {
            // TODO: Execute this case CheckMempoolCoinView !context.View.HaveCoins(txin.PrevOut.Hash) 
        }

        [Fact]
        public void AcceptToMemoryPool_TxInputsAreBad_ReturnsFalse()
        {
            // TODO: Execute this case CheckMempoolCoinView !context.View.HaveInputs(context.Transaction)
        }

        [Fact]
        public void AcceptToMemoryPool_NonBIP68CanMine_ReturnsFalse()
        {
            // TODO: Execute this case CreateMempoolEntry !CheckSequenceLocks(this.chain.Tip, context, PowConsensusValidator.StandardLocktimeVerifyFlags, context.LockPoints)
        }

        [Fact]
        public void AcceptToMemoryPool_NonStandardP2SH_ReturnsFalse()
        {
            // TODO: Execute failure cases for CreateMempoolEntry AreInputsStandard
        }

        [Fact]
        public void AcceptToMemoryPool_NonStandardP2WSH_ReturnsFalse()
        {
            // TODO: Execute failure cases for P2WSH Transactions CreateMempoolEntry
        }

        [Fact]
        public void AcceptToMemoryPool_TxExcessiveSigOps_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckSigOps
        }

        [Fact]
        public void AcceptToMemoryPool_TxFeeInvalid_ReturnsFalse()
        {
            // TODO: Execute failure cases for CheckFee
            // - less than minimum fee
            // - Insufficient priority for free transaction
            // - Absurdly high fee
        }

        [Fact]
        public void AcceptToMemoryPool_TxAncestorsInvalid_ReturnsFalse()
        {
            // TODO: Execute failure cases for CheckAncestors
            // - Too many ancestors
            // - conflicting spend transaction
        }

        [Fact]
        public void AcceptToMemoryPool_TxReplacementInsufficientFees_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckReplacement
        }

        #endregion

    }
}

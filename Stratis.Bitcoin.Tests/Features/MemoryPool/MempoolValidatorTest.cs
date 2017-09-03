using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Xunit;

namespace Stratis.Bitcoin.Tests.Features.MemoryPool
{
    /// <summary>
    /// Unit tests for the memory pool validator.
    /// </summary>
    /// <remarks>TODO: Currently only stubs - need to complete</remarks>
    public class MempoolValidatorTest
    {
        [Fact]
        public void CheckFinalTransaction_WithStandardLockTimeAndValidTxTime_ReturnsTrue()
        {
            // TODO: Implement test
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
            // TODO: Test case - lock point height exceeds chain height
        }

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

        [Fact]
        public async void AcceptToMemoryPool_WithValidP2PKHTxn_IsSuccessfull()
        {
            BitcoinSecret minerSecret = new BitcoinSecret(new Key(), Network.RegTest);
            ITestChainContext context = TestChainFactory.Create(Network.RegTest, minerSecret.PubKey.Hash.ScriptPubKey);
            IMempoolValidator validator = context.MempoolValidator;
            Assert.NotNull(validator);

            BitcoinSecret destSecret = new BitcoinSecret(new Key(), Network.RegTest);
            Transaction tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(context.SrcTxs[0].GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerSecret.PubKey)));
            tx.AddOutput(new TxOut(new Money(Money.CENT * 11), destSecret.PubKeyHash));
            tx.Sign(minerSecret, false);

            MempoolValidationState state = new MempoolValidationState(false);
            bool isSuccess = await validator.AcceptToMemoryPool(state, tx);
            Assert.True(isSuccess, "P2PKH tx not valid.");
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsCoinbase_ReturnsFalse()
        {
            // TODO: Execute this case PreMempoolChecks context.Transaction.IsCoinBase
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandardVersionUnsupported_ReturnsFalse()
        {
            // TODO:Test the cases in PreMempoolChecks CheckStandardTransaction
            // - Check version number
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandardTransactionSizeInvalid_ReturnsFalse()
        {
            // TODO:Test the cases in PreMempoolChecks CheckStandardTransaction
            // - Check transaction size
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandardInputScriptSigsLengthInvalid_ReturnsFalse()
        {
            // TODO:Test the cases in PreMempoolChecks CheckStandardTransaction
            // - Check input scriptsig lengths
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandardScriptSigIsPushOnly_ReturnsFalse()
        {
            // TODO:Test the cases in PreMempoolChecks CheckStandardTransaction
            // - Check input scriptsig push only            
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandardScriptTemplateIsNull_ReturnsFalse()
        {
            // TODO:Test the casses in PreMempoolChecks CheckStandardTransaction
            // - Check output scripttemplate == null
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandardOutputIsDust_ReturnsFalse()
        {
            // TODO:Test the cases in PreMempoolChecks CheckStandardTransaction
            // - Check output dust
        }

        [Fact]
        public void AcceptToMemoryPool_TxIsNonStandardOutputNotSingleReturn_ReturnsFalse()
        {
            // TODO:Test the cases in PreMempoolChecks CheckStandardTransaction
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
        public void AcceptToMemoryPool_TxFeeInvalidLessThanMin_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckFee
            // - less than minimum fee
        }

        [Fact]
        public void AcceptToMemoryPool_TxFeeInvalidInsufficentPriorityForFree_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckFee
            // - Insufficient priority for free transaction
        }

        [Fact]
        public void AcceptToMemoryPool_TxFeeInvalidAbsurdlyHighFee_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckFee
            // - Absurdly high fee
        }

        [Fact]
        public void AcceptToMemoryPool_TxTooManyAncestors_ReturnsFalse()
        {
            // TODO: Execute failure cases for CheckAncestors
            // - Too many ancestors
        }

        [Fact]
        public void AcceptToMemoryPool_TxAncestorsConflictSpend_ReturnsFalse()
        {
            // TODO: Execute failure cases for CheckAncestors
            // - conflicting spend transaction
        }

        [Fact]
        public void AcceptToMemoryPool_TxReplacementInsufficientFees_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckReplacement InsufficientFees
            // - three separate logic checks inside CheckReplacement
        }

        [Fact]
        public void AcceptToMemoryPool_TxReplacementTooManyReplacements_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckReplacement TooManyPotentialReplacements
        }

        [Fact]
        public void AcceptToMemoryPool_TxReplacementAddsUnconfirmed_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckReplacement ReplacementAddsUnconfirmed
        }

        [Fact]
        public void AcceptToMemoryPool_TxPowConsensusCheckInputNegativeOrOverflow_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs PowConsensusValidator.CheckInputs BadTransactionInputValueOutOfRange
        }

        [Fact]
        public void AcceptToMemoryPool_TxPowConsensusCheckInputBadTransactionInBelowOut_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs PowConsensusValidator.CheckInputs BadTransactionInBelowOut
        }

        [Fact]
        public void AcceptToMemoryPool_TxPowConsensusCheckInputNegativeFee_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs PowConsensusValidator.CheckInputs NegativeFee
        }

        [Fact]
        public void AcceptToMemoryPool_TxPowConsensusCheckInputFeeOutOfRange_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs PowConsensusValidator.CheckInputs FeeOutOfRange
        }

        [Fact]
        public void AcceptToMemoryPool_TxVerifyStandardScriptConsensusFailure_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs VerifyScriptConsensus for ScriptVerify.Standard
        }

        [Fact]
        public void AcceptToMemoryPool_TxContextVerifyStandardScriptFailure_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs ctx.VerifyScript for ScriptVerify.Standard
        }

        [Fact]
        public void AcceptToMemoryPool_TxVerifyP2SHScriptConsensusFailure_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs VerifyScriptConsensus for ScriptVerify.P2SH
        }

        [Fact]
        public void AcceptToMemoryPool_TxContextVerifyP2SHScriptFailure_ReturnsFalse()
        {
            // TODO: Execute failure case for CheckAllInputs CheckInputs ctx.VerifyScript for ScriptVerify.P2SH
        }

        [Fact]
        public void AcceptToMemoryPool_MemPoolFull_ReturnsFalse()
        {
            // TODO: Execute failure case for this check after trimming mempool !this.memPool.Exists(context.TransactionHash)
        }
    }
}

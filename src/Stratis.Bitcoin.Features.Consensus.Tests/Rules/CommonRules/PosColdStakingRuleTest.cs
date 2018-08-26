using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosColdStakingRuleTest : PosConsensusRuleUnitTestBase
    {
        /// <summary>
        /// Initializes the unspent output set and creates the block to validate.
        /// </summary>
        public PosColdStakingRuleTest()
        {
            (this.ruleContext as UtxoRuleContext).UnspentOutputSet = new UnspentOutputSet();
            (this.ruleContext as UtxoRuleContext).UnspentOutputSet.SetCoins(new UnspentOutputs[0]);

            this.ruleContext.ValidationContext.BlockToValidate = this.network.Consensus.ConsensusFactory.CreateBlock();
        }

        /// <summary>
        /// This helper tests various scenarios related to the PosColdStakingRule. The parameters determine the test cases.
        /// </summary>
        /// <param name="isColdCoinStake">Tests the scenario where a hotPubKey was used to spend a cold stake transaction input.</param>
        /// <param name="inputScriptPubKeysDiffer">Tests the scenario where some of the input scriptPubKeys differ.</param>
        /// <param name="outputScriptPubKeysDiffer">Tests the scenario where some of the output scriptPubKeys differ.</param>
        /// <param name="badSecondOutput">Tests the scenario where the second output is not an OP_RETURN followed by some data.</param>
        /// <param name="inputsExceedOutputs">Tests the scenario where the output amount exceeds the input amount.</param>
        /// <param name="expectedError">The error expected by running this test. Set to null if no error is expected.</param>
        private async Task PosColdStakingRuleTestHelperAsync(bool isColdCoinStake, bool inputScriptPubKeysDiffer, bool outputScriptPubKeysDiffer,
            bool badSecondOutput, bool inputsExceedOutputs, ConsensusError expectedError)
        {
            var rule = this.CreateRule<PosColdStakingRule>();

            Block block = this.ruleContext.ValidationContext.BlockToValidate;

            // Create two scripts that are different.
            var scriptPubKey1 = new Script(OpcodeType.OP_1);
            var scriptPubKey2 = new Script(OpcodeType.OP_2);

            // Add dummy first transaction.
            var transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            block.Transactions.Add(transaction);

            // Create a previous transaction with scriptPubKey outputs.
            var prevTransaction = this.network.CreateTransaction();
            prevTransaction.Outputs.Add(new TxOut(15, scriptPubKey1));
            prevTransaction.Outputs.Add(new TxOut(25, inputScriptPubKeysDiffer ? scriptPubKey2: scriptPubKey1));

            // Record the spendable outputs.
            (this.ruleContext as UtxoRuleContext).UnspentOutputSet.Update(prevTransaction, 0);

            // Create cold coin stake transaction.
            transaction = this.network.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(prevTransaction, 0),
                ScriptSig = new Script()
            });

            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(prevTransaction, 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            transaction.Outputs.Add(new TxOut(Money.Zero, badSecondOutput ?
                new Script() : new Script(OpcodeType.OP_RETURN, Op.GetPushOp(new Key().PubKey.Compress().ToBytes()))));
            transaction.Outputs.Add(new TxOut(inputsExceedOutputs ? 10 : 15, scriptPubKey1));
            transaction.Outputs.Add(new TxOut(25, outputScriptPubKeysDiffer ? scriptPubKey2 : scriptPubKey1));

            (transaction as PosTransaction).IsColdCoinStake = isColdCoinStake;

            block.Transactions.Add(transaction);

            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Time = (uint)1483747200;
            block.Transactions[0].Time = (uint)1483747200;
            block.Transactions[1].Time = (uint)1483747200;
            block.UpdateMerkleRoot();

            Assert.True(BlockStake.IsProofOfStake(block));

            this.concurrentChain.SetTip(block.Header);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.concurrentChain.Tip;

            if (expectedError != null)
            {
                ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => rule.RunAsync(this.ruleContext).GetAwaiter().GetResult());

                Assert.Equal(expectedError, exception.ConsensusError);

                return;
            }

            await rule.RunAsync(this.ruleContext);
        }

        /// <summary>
        /// Create a transaction where all inputs of this transaction are not using the same ScriptPubKey. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeValidBlockDoesNotThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: false, expectedError: null);
        }

        /// <summary>
        /// Create a transaction where some of the outputs (except for the marker output and the pubkey output) are using a different ScriptPubKey
        /// from the input transactions. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithMismatchingScriptPubKeyInputsThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: true, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: false, expectedError: ConsensusErrors.BadColdstakeInputs);
        }

        /// <summary>
        /// Create a transaction where some of the outputs (except for the marker output and the pubkey output) are using a different ScriptPubKey
        /// from the input transactions. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithMismatchingScriptPubKeyOutputsThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: true,
                badSecondOutput: false, inputsExceedOutputs: false, expectedError: ConsensusErrors.BadColdstakeOutputs);
        }

        /// <summary>
        /// Create a transaction that has a second output that is not an OP_RETURN followed by data. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithBadSecondOutputThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: true, inputsExceedOutputs: false, expectedError: ConsensusErrors.BadColdstakeOutputs);
        }

        /// <summary>
        /// Create a transaction that meets the above criteria but the sum of values of all inputs is greater than the sum of values of all
        /// outputs. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithOutputsExceedingInputsThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: true, expectedError: ConsensusErrors.BadColdstakeAmount);
        }

        /// <summary>
        /// Create a transaction that is not a cold coin stake transaction. The validation should succeed.
        /// </summary>
        [Fact]
        public async Task PosCoinStakeWhichIsNotColdCoinStakeDoesNotThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: false, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: false, expectedError: null);
        }
    }
}

using System.Linq;
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
        /// This helper tests various scenarios related to the <see cref="PosColdStakingRule"/>. The parameters determine the test cases.
        /// </summary>
        /// <param name="isColdCoinStake">Tests the scenario where a hotPubKey was used to spend a cold stake transaction input.</param>
        /// <param name="inputScriptPubKeysDiffer">Tests the scenario where some of the input scriptPubKeys differ.</param>
        /// <param name="outputScriptPubKeysDiffer">Tests the scenario where some of the output scriptPubKeys differ.</param>
        /// <param name="badSecondOutput">Tests the scenario where the second output is not an OP_RETURN followed by some data.</param>
        /// <param name="inputsExceedOutputs">Tests the scenario where the input amount exceeds the output amount.</param>
        /// <param name="inputsWithoutOutputs">Tests the scenario where the some inputs have no incoming outputs.</param>
        /// <param name="expectedError">The error expected by running this test. Set to <c>null</c> if no error is expected.</param>
        private async Task PosColdStakingRuleTestHelperAsync(bool isColdCoinStake, bool inputScriptPubKeysDiffer, bool outputScriptPubKeysDiffer,
            bool badSecondOutput, bool inputsExceedOutputs, bool inputsWithoutOutputs, ConsensusError expectedError)
        {
            Block block = this.ruleContext.ValidationContext.BlockToValidate;

            // Create two scripts that are different.
            var scriptPubKey1 = new Script(OpcodeType.OP_1);
            var scriptPubKey2 = new Script(OpcodeType.OP_2);

            // Add dummy first transaction.
            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            block.Transactions.Add(transaction);

            // Create a previous transaction with scriptPubKey outputs.
            Transaction prevTransaction = this.network.CreateTransaction();
            prevTransaction.Outputs.Add(new TxOut(15, scriptPubKey1));
            prevTransaction.Outputs.Add(new TxOut(25, inputScriptPubKeysDiffer ? scriptPubKey2 : scriptPubKey1));

            // Record the spendable outputs.
            var posRuleContext = this.ruleContext as PosRuleContext;
            posRuleContext.UnspentOutputSet.Update(prevTransaction, 0);

            // Create cold coin stake transaction.
            Transaction coinstakeTransaction = this.network.CreateTransaction();
            coinstakeTransaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(prevTransaction, 0),
                ScriptSig = new Script()
            });

            coinstakeTransaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(prevTransaction, inputsWithoutOutputs ? 2 : 1),
                ScriptSig = new Script()
            });

            coinstakeTransaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            coinstakeTransaction.Outputs.Add(new TxOut(Money.Zero, badSecondOutput ?
                new Script() : new Script(OpcodeType.OP_RETURN, Op.GetPushOp(new Key().PubKey.Compress().ToBytes()))));
            coinstakeTransaction.Outputs.Add(new TxOut(inputsExceedOutputs ? 10 : 15, scriptPubKey1));
            coinstakeTransaction.Outputs.Add(new TxOut(25, outputScriptPubKeysDiffer ? scriptPubKey2 : scriptPubKey1));

            // Set this flag which is expected to be set by the preceding PosCoinview rule if this were run in an integrated scenario.
            (coinstakeTransaction as PosTransaction).IsColdCoinStake = isColdCoinStake;

            block.Transactions.Add(coinstakeTransaction);

            // Finalize the block and add it to the chain.
            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Time = (uint)1483747200;
            block.Transactions[0].Time = (uint)1483747200;
            block.Transactions[1].Time = (uint)1483747200;
            block.UpdateMerkleRoot();

            Assert.True(BlockStake.IsProofOfStake(block));

            this.concurrentChain.SetTip(block.Header);

            // Execute the rule and check the outcome against what is expected.
            var rule = this.CreateRule<PosColdStakingRule>();

            // Initialize the rule context.
            posRuleContext.ValidationContext.ChainedHeaderToValidate = this.concurrentChain.Tip;
            posRuleContext.CoinStakePrevOutputs = coinstakeTransaction.Inputs.ToDictionary(txin => txin, txin => posRuleContext.UnspentOutputSet.GetOutputFor(txin));
            posRuleContext.TotalCoinStakeValueIn = posRuleContext.CoinStakePrevOutputs.Sum(a => a.Value?.Value ?? 0);

            // If an error is expeected then capture the error and compare it against the expected error.
            if (expectedError != null)
            {
                ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => rule.RunAsync(this.ruleContext).GetAwaiter().GetResult());

                Assert.Equal(expectedError, exception.ConsensusError);

                return;
            }

            // No error is expected. Attempt to run the rule normally.
            await rule.RunAsync(this.ruleContext);
        }

        /// <summary>
        /// Create a transaction where all inputs and outputs of this transaction are using the same ScriptPubKeys. Also, the second output is as
        /// expected (an OP_RETURN followed by a compressed public key) and the input does not exceed the output. No exception should be thrown.
        /// </summary>
        [Fact]
        public async Task PosColdStakeValidBlockDoesNotThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: false, inputsWithoutOutputs: false, expectedError: null);
        }

        /// <summary>
        /// Create a transaction where all inputs of this transaction are not using the same ScriptPubKeys. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithMismatchingScriptPubKeyInputsThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: true, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: false, inputsWithoutOutputs: false, expectedError: ConsensusErrors.BadColdstakeInputs);
        }

        /// <summary>
        /// Create a transaction where some of the outputs (except for the marker output and the pubkey output) are using a different ScriptPubKeys
        /// from the input transactions. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithMismatchingScriptPubKeyOutputsThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: true,
                badSecondOutput: false, inputsExceedOutputs: false, inputsWithoutOutputs: false, expectedError: ConsensusErrors.BadColdstakeOutputs);
        }

        /// <summary>
        /// Create a transaction that has a second output that is not an OP_RETURN followed by data. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithBadSecondOutputThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: true, inputsExceedOutputs: false, inputsWithoutOutputs: false, expectedError: ConsensusErrors.BadColdstakeOutputs);
        }

        /// <summary>
        /// Create a transaction that meets the above criteria but the sum of values of all inputs is greater than the sum of values of all
        /// outputs. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosColdStakeWithOutputsExceedingInputsThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: true, inputsWithoutOutputs: false, expectedError: ConsensusErrors.BadColdstakeAmount);
        }

        /// <summary>
        /// Create a transaction that is not a cold coin stake transaction but would otherwise fail all the tests. The validation should succeed.
        /// </summary>
        [Fact]
        public async Task PosCoinStakeWhichIsNotColdCoinStakeDoesNotThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: false, inputScriptPubKeysDiffer: true, outputScriptPubKeysDiffer: true,
                badSecondOutput: true, inputsExceedOutputs: true, inputsWithoutOutputs: false, expectedError: null);
        }

        /// <summary>
        /// Create a transaction that is a cold coin stake transaction that has inputs without outputs. The validation should fail.
        /// </summary>
        [Fact]
        public async Task PosCoinStakeWhichHasInputsWithoutOutputsThrowExceptionAsync()
        {
            await PosColdStakingRuleTestHelperAsync(isColdCoinStake: true, inputScriptPubKeysDiffer: false, outputScriptPubKeysDiffer: false,
                badSecondOutput: false, inputsExceedOutputs: false, inputsWithoutOutputs: true, expectedError: ConsensusErrors.BadColdstakeInputs);
        }
    }
}

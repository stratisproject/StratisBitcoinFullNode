using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus.Rules;
namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// This rule performs further coldstaking transaction validation when cold staking balances are spent with a 
    /// hot wallet address (versus the cold wallet address) inside of a coldstaking transaction.
    /// </summary>
    /// <remarks>
    /// This code will perform further validation for transactions that spend the new scriptPubKey containing the
    /// new <see cref="OpcodeType.OP_CHECKCOLDSTAKEVERIFY"/> opcode using a hotPubKeyHash inside of a coinstake 
    /// transaction. Those are the conditions under which the <see cref="PosTransaction.IsColdCoinStake"/> flag will 
    /// be set and it is therefore the flag we use to determine if these rules should be applied. Due to a check 
    /// being done inside of the implementation of the <see cref="OpcodeType.OP_CHECKCOLDSTAKEVERIFY"/> opcode this 
    /// flag can only be set for coinstake transactions after the opcode implementation has been activated (at a 
    /// specified block height). The opcode activation flag is <see cref="ScriptVerify.CheckColdStakeVerify"/> and 
    /// it is set in <see cref="DeploymentFlags.ScriptFlags"/> when the block height is greater than or equal to <see 
    /// cref="PosConsensusOptions.ColdStakingActivationHeight"/>.
    /// 
    /// The following conditions are enforced for cold staking transactions. This rule implements all but the first one:
    /// <list type="number">
    /// <item>Check if the transaction spending an output, which contains this instruction, is a coinstake transaction.
    /// If it is not, the script fails. This has already been checked within the opcode implementation before setting 
    /// <see cref="PosTransaction.IsColdCoinStake"/> so it will not be checked here.</item>
    /// <item>Check that ScriptPubKeys of all inputs of this transaction are the same. If they are not, the script 
    /// fails.</item>
    /// <item>Check that ScriptPubKeys of all outputs of this transaction, except for the marker output (a special 
    /// first output of each coinstake transaction) and the pubkey output (an optional special second output that
    /// contains public key in coinstake transaction), are the same as ScriptPubKeys of the inputs. If they are not,
    /// the script fails.</item>
    /// <item>Check that the sum of values of all inputs is smaller or equal to the sum of values of all outputs. If 
    /// this does not hold, the script fails.</item>
    /// </list>
    /// </remarks>
    public class PosColdStakingRule : UtxoStoreConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadColdstakeInputs">Thrown if the input scriptPubKeys mismatch.</exception>
        /// <exception cref="ConsensusErrors.BadColdstakeOutputs">Thrown if the output scriptPubKeys mismatch.</exception>
        /// <exception cref="ConsensusErrors.BadColdstakeAmount">Thrown if the total input is smaller or equal than the sum of outputs.</exception>
        public override Task RunAsync(RuleContext context)
        {
            this.Logger.LogTrace("()");

            // Take the coinstake transaction from the block and check if the flag ("IsColdCoinStake") is set.
            // The flag will only be set in the OP_CHECKCOLDSTAKEVERIFY is ScriptFlags in DeploymentFlags has "CheckColdStakeVerify" set.
            Block block = context.ValidationContext.BlockToValidate;
            if ((block.Transactions.Count >= 2) && (block.Transactions[1] is PosTransaction posTran) && posTran.IsColdCoinStake)
            {
                var utxoRuleContext = context as UtxoRuleContext;
                UnspentOutputSet view = utxoRuleContext.UnspentOutputSet;

                // Check that ScriptPubKeys of all inputs of this transaction are the same. If they are not, the script fails.
                Script scriptPubKey = null;
                if (posTran.Inputs.Count >= 1)
                {
                    scriptPubKey = view.GetOutputFor(posTran.Inputs[0]).ScriptPubKey;
                    if (posTran.Inputs.Skip(1).Where(i => view.GetOutputFor(i).ScriptPubKey != scriptPubKey).Any())
                    {
                        this.Logger.LogTrace("(-)[BAD_COLDSTAKE_INPUTS]");
                        ConsensusErrors.BadColdstakeInputs.Throw();
                    }
                }

                // Check that ScriptPubKeys of all outputs of this transaction, except for the marker output(a special first
                // output of each coinstake transaction) and the pubkey output (an optional special second output that contains 
                // public key in coinstake transaction), are the same as ScriptPubKeys of the inputs. If they are not, the script fails.
                // Assume that the presence of the second output will be confirmed by the block signature rule.
                if (posTran.Outputs.Skip(2).Where(o => o.ScriptPubKey != scriptPubKey).Any())
                {
                    this.Logger.LogTrace("(-)[BAD_COLDSTAKE_OUTPUTS]");
                    ConsensusErrors.BadColdstakeOutputs.Throw();
                }

                // Check that the sum of values of all inputs is smaller or equal to the sum of values of all outputs. If this does
                // not hold, the script fails.
                var posRuleContext = context as PosRuleContext;
                Money stakeReward = posTran.TotalOut - posRuleContext.TotalCoinStakeValueIn;
                if (stakeReward < 0)
                {
                    this.Logger.LogTrace("(-)[BAD_COLDSTAKE_AMOUNT]");
                    ConsensusErrors.BadColdstakeAmount.Throw();
                }
            }
            else
            {
                this.Logger.LogTrace("POS cold staking validation skipped for checkpointed block at height {0}.",
                    context.ValidationContext.ChainedHeaderToValidate.Height);
            }

            this.Logger.LogTrace("(-)");

            return Task.CompletedTask;
        }
    }
}
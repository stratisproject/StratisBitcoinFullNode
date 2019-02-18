using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// If a transaction's inputs contain an OP_SPEND opcode in the scriptsig, check that the transaction
    /// that occurs directly before contains OP_CREATE or OP_CALL in its outputs. In conjunction with
    /// <see cref="MempoolOpSpendRule"/>, ensures that only a contract execution transaction is able to
    /// create OP_SPEND inputs.
    /// </summary>
    public class OpSpendRule : FullValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            for (var i = 0; i < block.Transactions.Count; i++)
            {
                var transaction = block.Transactions[i];

                // If the inputs to the transaction do not contain an OP_SPEND, continue.
                if (!transaction.IsSmartContractSpendTransaction())
                    continue;

                // If i == 0, there can be no previous OP_CALL or OP_CREATE, so OP_SPEND is invalid.
                if (i == 0)
                {
                    this.Throw();
                };

                Transaction previousTransaction = block.Transactions[i - 1];

                // Check for OP_CREATE and OP_CALL outputs because both opcodes can be followed by an OP_SPEND input.
                if (!previousTransaction.IsSmartContractExecTransaction())
                {
                    this.Throw();
                }
            }

            return Task.CompletedTask;
        }

        private void Throw()
        {
            new ConsensusError("opspend-did-not-follow-opcall", "transaction contained an op-spend that did not follow an op-call").Throw();
        }
    }
}
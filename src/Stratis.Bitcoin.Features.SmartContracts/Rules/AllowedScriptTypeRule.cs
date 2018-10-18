using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Enforces that only certain script types are used on the network.
    /// </summary>
    public class AllowedScriptTypeRule : PartialValidationConsensusRule, ISmartContractMempoolRule
    {
        // TODO: Sidechains will need to allow their script type in here.

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            foreach (Transaction transaction in block.Transactions)
            {
                CheckTransaction(transaction);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            CheckTransaction(context.Transaction);
        }

        private void CheckTransaction(Transaction transaction)
        {
            // Why dodge coinbase?
            // 1) Coinbase can only be written by Authority nodes anyhow.
            // 2) Coinbase inputs look weird, are tough to validate.
            if (!transaction.IsCoinBase)
            {
                foreach (TxOut output in transaction.Outputs)
                {
                    CheckOutput(output);
                }

                foreach (TxIn input in transaction.Inputs)
                {
                    CheckInput(input);
                }
            }
        }

        private void CheckOutput(TxOut output)
        {
            if (output.ScriptPubKey.IsSmartContractExec())
                return;

            if (output.ScriptPubKey.IsSmartContractInternalCall())
                return;

            if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                return;

            new ConsensusError("disallowed-output-script", "Only P2PKH and smart contract scripts are allowed.").Throw();
        }

        private void CheckInput(TxIn input)
        {
            if (input.ScriptSig.IsSmartContractSpend())
                return;

            if (PayToPubkeyHashTemplate.Instance.CheckScriptSig(this.Parent.Network, input.ScriptSig))
                return;

            new ConsensusError("disallowed-input-script", "Only P2PKH and smart contract scripts are allowed.").Throw();
        }
    }
}

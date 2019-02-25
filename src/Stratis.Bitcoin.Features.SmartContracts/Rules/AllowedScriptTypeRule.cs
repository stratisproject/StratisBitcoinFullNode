using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Enforces that only certain script types are used on the network.
    /// </summary>
    public class AllowedScriptTypeRule : PartialValidationConsensusRule, ISmartContractMempoolRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransaction(transaction);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            this.CheckTransaction(context.Transaction);
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
                    this.CheckOutput(output);
                }

                foreach (TxIn input in transaction.Inputs)
                {
                    this.CheckInput(input);
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

            // For cross-chain transfers
            if (PayToScriptHashTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                return;

            // For cross-chain transfers
            if (PayToMultiSigTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                return;

            // For cross-chain transfers
            if (TxNullDataTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                return;

            new ConsensusError("disallowed-output-script", "Only the following script types are allowed on smart contracts network: P2PKH, P2SH, P2MultiSig, OP_RETURN and smart contracts").Throw();
        }

        private void CheckInput(TxIn input)
        {
            if (input.ScriptSig.IsSmartContractSpend())
                return;

            if (PayToPubkeyHashTemplate.Instance.CheckScriptSig(this.Parent.Network, input.ScriptSig))
                return;

            // Currently necessary to spend premine. Could be stricter.
            if (PayToPubkeyTemplate.Instance.CheckScriptSig(this.Parent.Network, input.ScriptSig, null))
                return;

            if (PayToScriptHashTemplate.Instance.CheckScriptSig(this.Parent.Network, input.ScriptSig, null))
                return;

            // For cross-chain transfers
            if (PayToMultiSigTemplate.Instance.CheckScriptSig(this.Parent.Network, input.ScriptSig, null))
                return;

            new ConsensusError("disallowed-input-script", "Only the following script types are allowed on smart contracts network: P2PKH, P2SH, P2MultiSig, OP_RETURN and smart contracts").Throw();
        }
    }
}

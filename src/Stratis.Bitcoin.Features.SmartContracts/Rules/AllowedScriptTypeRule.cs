using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Enforces that only certain script types are used on the network.
    /// </summary>
    public class AllowedScriptTypeRule : PartialValidationConsensusRule
    {
        private readonly Network network;

        public AllowedScriptTypeRule(Network network)
        {
            this.network = network;
        }

        /// <inheritdoc/>
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            foreach (Transaction transaction in block.Transactions)
            {
                CheckTransaction(this.network, transaction);
            }

            return Task.CompletedTask;
        }

        public static void CheckTransaction(Network network, Transaction transaction)
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
                    CheckInput(network, input);
                }
            }
        }

        private static void CheckOutput(TxOut output)
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

        private static void CheckInput(Network network, TxIn input)
        {
            if (input.ScriptSig.IsSmartContractSpend())
                return;

            if (PayToPubkeyHashTemplate.Instance.CheckScriptSig(network, input.ScriptSig))
                return;

            // Currently necessary to spend premine. Could be stricter.
            if (PayToPubkeyTemplate.Instance.CheckScriptSig(network, input.ScriptSig, null))
                return;

            if (PayToScriptHashTemplate.Instance.CheckScriptSig(network, input.ScriptSig, null))
                return;

            // For cross-chain transfers
            if (PayToMultiSigTemplate.Instance.CheckScriptSig(network, input.ScriptSig, null))
                return;

            new ConsensusError("disallowed-input-script", "Only the following script types are allowed on smart contracts network: P2PKH, P2SH, P2MultiSig, OP_RETURN and smart contracts").Throw();
        }
    }
}

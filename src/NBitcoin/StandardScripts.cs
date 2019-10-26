using System.Collections.Generic;
using System.Linq;
using NBitcoin.BitcoinCore;
using NBitcoin.Policy;

namespace NBitcoin
{
    public static class StandardScripts
    {
        private static readonly List<ScriptTemplate> standardTemplates = new List<ScriptTemplate>
        {
            PayToPubkeyHashTemplate.Instance,
            PayToPubkeyTemplate.Instance,
            PayToScriptHashTemplate.Instance,
            PayToMultiSigTemplate.Instance,
            TxNullDataTemplate.Instance,
            PayToWitTemplate.Instance
        };

        /// <summary>
        /// Registers a new standard script template if it does not exist yet based on <see cref="ScriptTemplate.Type"/>.
        /// </summary>
        /// <param name="scriptTemplate">The standard script template to register.</param>
        public static void RegisterStandardScriptTemplate(ScriptTemplate scriptTemplate)
        {
            if (!standardTemplates.Any(template => (template.Type == scriptTemplate.Type)))
                standardTemplates.Add(scriptTemplate);
        }

        public static bool IsStandardTransaction(Transaction tx, Network network)
        {
            return new StandardTransactionPolicy(network).Check(tx, null).Length == 0;
        }

        public static bool AreOutputsStandard(Network network, Transaction tx)
        {
            return tx.Outputs.All(vout => IsStandardScriptPubKey(network, vout.ScriptPubKey));
        }

        public static ScriptTemplate GetTemplateFromScriptPubKey(Script script)
        {
            return standardTemplates.FirstOrDefault(t => t.CheckScriptPubKey(script));
        }

        public static bool IsStandardScriptPubKey(Network network, Script scriptPubKey)
        {
            return standardTemplates.Any(template => template.CheckScriptPubKey(scriptPubKey));
        }

        private static bool IsStandardScriptSig(Network network, Script scriptSig, Script scriptPubKey)
        {
            ScriptTemplate template = GetTemplateFromScriptPubKey(scriptPubKey);
            if (template == null)
                return false;

            return template.CheckScriptSig(network, scriptSig, scriptPubKey);
        }

        // Check transaction inputs, and make sure any
        // pay-to-script-hash transactions are evaluating IsStandard scripts
        //
        // Why bother? To avoid denial-of-service attacks; an attacker
        // can submit a standard HASH... OP_EQUAL transaction,
        // which will get accepted into blocks. The redemption
        // script can be anything; an attacker could use a very
        // expensive-to-check-upon-redemption script like:
        //   DUP CHECKSIG DROP ... repeated 100 times... OP_1
        public static bool AreInputsStandard(Network network, Transaction tx, CoinsView coinsView)
        {
            if (tx.IsCoinBase)
                return true; // Coinbases don't use vin normally

            foreach (TxIn input in tx.Inputs)
            {
                TxOut prev = coinsView.GetOutputFor(input);
                if (prev == null)
                    return false;

                if (!IsStandardScriptSig(network, input.ScriptSig, prev.ScriptPubKey))
                    return false;
            }

            return true;
        }
    }
}
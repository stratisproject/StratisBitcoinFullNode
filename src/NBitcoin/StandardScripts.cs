using System.Linq;
using NBitcoin.BitcoinCore;
using NBitcoin.Policy;

namespace NBitcoin
{

    public static class StandardScripts
    {
        static readonly ScriptTemplate[] _StandardTemplates = new ScriptTemplate[]
        {
            PayToPubkeyHashTemplate.Instance,
            PayToPubkeyTemplate.Instance,
            PayToScriptHashTemplate.Instance,
            PayToMultiSigTemplate.Instance,
            TxNullDataTemplate.Instance,
            PayToWitTemplate.Instance
        };

        public static bool IsStandardTransaction(Transaction tx, Network network = null)
        {
            network = network ?? Network.Main;

            return new StandardTransactionPolicy(network).Check(tx, null).Length == 0;
        }

        public static bool AreOutputsStandard(Network network, Transaction tx)
        {
            return tx.Outputs.All(vout => IsStandardScriptPubKey(network, vout.ScriptPubKey));
        }

        public static ScriptTemplate GetTemplateFromScriptPubKey(Network network, Script script)
        {
            return _StandardTemplates.FirstOrDefault(t => t.CheckScriptPubKey(network, script));
        }

        public static bool IsStandardScriptPubKey(Network network, Script scriptPubKey)
        {
            return _StandardTemplates.Any(template => template.CheckScriptPubKey(network, scriptPubKey));
        }
        private static bool IsStandardScriptSig(Network network, Script scriptSig, Script scriptPubKey)
        {
            var template = GetTemplateFromScriptPubKey(network, scriptPubKey);
            if(template == null)
                return false;

            return template.CheckScriptSig(network, scriptSig, scriptPubKey);
        }

        //
        // Check transaction inputs, and make sure any
        // pay-to-script-hash transactions are evaluating IsStandard scripts
        //
        // Why bother? To avoid denial-of-service attacks; an attacker
        // can submit a standard HASH... OP_EQUAL transaction,
        // which will get accepted into blocks. The redemption
        // script can be anything; an attacker could use a very
        // expensive-to-check-upon-redemption script like:
        //   DUP CHECKSIG DROP ... repeated 100 times... OP_1
        //
        public static bool AreInputsStandard(Network network, Transaction tx, CoinsView coinsView)
        {
            if(tx.IsCoinBase)
                return true; // Coinbases don't use vin normally

            foreach(var input in tx.Inputs)
            {
                TxOut prev = coinsView.GetOutputFor(input);
                if(prev == null)
                    return false;
                if(!IsStandardScriptSig(network, input.ScriptSig, prev.ScriptPubKey))
                    return false;
            }

            return true;
        }
    }
}

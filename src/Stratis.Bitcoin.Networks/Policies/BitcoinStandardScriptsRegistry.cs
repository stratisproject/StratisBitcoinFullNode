using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.Networks.Policies
{
    /// <summary>
    /// Bitcoin-specific standard transaction definitions.
    /// </summary>
    public class BitcoinStandardScriptsRegistry : StandardScriptsRegistry
    {
        // See MAX_OP_RETURN_RELAY in Bitcoin Core, <script/standard.h.>
        // 80 bytes of data, +1 for OP_RETURN, +2 for the pushdata opcodes.
        public const int MaxOpReturnRelay = 83;

        // Need a network-specific version of the template list
        private readonly List<ScriptTemplate> standardTemplates = new List<ScriptTemplate>
        {
            PayToPubkeyHashTemplate.Instance,
            PayToPubkeyTemplate.Instance,
            PayToScriptHashTemplate.Instance,
            PayToMultiSigTemplate.Instance,
            new TxNullDataTemplate(MaxOpReturnRelay),
            PayToWitTemplate.Instance
        };

        public override void RegisterStandardScriptTemplate(ScriptTemplate scriptTemplate)
        {
            if (!this.standardTemplates.Any(template => (template.Type == scriptTemplate.Type)))
            {
                this.standardTemplates.Add(scriptTemplate);
            }
        }

        public override bool IsStandardTransaction(Transaction tx, Network network)
        {
            return base.IsStandardTransaction(tx, network);
        }

        public override bool AreOutputsStandard(Network network, Transaction tx)
        {
            return base.AreOutputsStandard(network, tx);
        }

        public override ScriptTemplate GetTemplateFromScriptPubKey(Script script)
        {
            return this.standardTemplates.FirstOrDefault(t => t.CheckScriptPubKey(script));
        }

        public override bool IsStandardScriptPubKey(Network network, Script scriptPubKey)
        {
            return base.IsStandardScriptPubKey(network, scriptPubKey);
        }

        public override bool AreInputsStandard(Network network, Transaction tx, CoinsView coinsView)
        {
            return base.AreInputsStandard(network, tx, coinsView);
        }
    }
}

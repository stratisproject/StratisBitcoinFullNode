using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.BitcoinCore;

namespace NBitcoin
{
    /// <summary>
    /// Injected proxy to <see cref="StandardScripts"/>.
    /// </summary>
    public class StandardScriptsRegistry : IStandardScriptsRegistry
    {
        /// <summary>
        /// Registers a new standard script template if it does not exist yet based on <see cref="ScriptTemplate.Type"/>.
        /// </summary>
        /// <param name="scriptTemplate">The standard script template to register.</param>
        public virtual void RegisterStandardScriptTemplate(ScriptTemplate scriptTemplate)
        {
            StandardScripts.RegisterStandardScriptTemplate(scriptTemplate);
        }

        public virtual bool IsStandardTransaction(Transaction tx, Network network)
        {
            return StandardScripts.IsStandardTransaction(tx, network);
        }

        public virtual bool AreOutputsStandard(Network network, Transaction tx)
        {
            return StandardScripts.AreOutputsStandard(network, tx);
        }

        public virtual ScriptTemplate GetTemplateFromScriptPubKey(Script script)
        {
            return StandardScripts.GetTemplateFromScriptPubKey(script);
        }

        public virtual bool IsStandardScriptPubKey(Network network, Script scriptPubKey)
        {
            return StandardScripts.IsStandardScriptPubKey(network, scriptPubKey);
        }

        public virtual bool AreInputsStandard(Network network, Transaction tx, CoinsView coinsView)
        {
            return StandardScripts.AreInputsStandard(network, tx, coinsView);
        }
    }
}

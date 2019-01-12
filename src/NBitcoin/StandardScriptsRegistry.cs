﻿using System;
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

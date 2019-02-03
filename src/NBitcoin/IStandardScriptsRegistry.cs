using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.BitcoinCore;

namespace NBitcoin
{
    public interface IStandardScriptsRegistry
    {
        void RegisterStandardScriptTemplate(ScriptTemplate scriptTemplate);

        bool IsStandardTransaction(Transaction tx, Network network);

        bool AreOutputsStandard(Network network, Transaction tx);

        ScriptTemplate GetTemplateFromScriptPubKey(Script script);

        bool IsStandardScriptPubKey(Network network, Script scriptPubKey);

        bool AreInputsStandard(Network network, Transaction tx, CoinsView coinsView);
    }
}

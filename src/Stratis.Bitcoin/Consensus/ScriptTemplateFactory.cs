using NBitcoin;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Consensus
{
    public class ScriptTemplateFactory : IScriptTemplateFactory
    {
        public ScriptTemplate GetTemplateFromScriptPubKey(Network network, Script script)
        {
            return StandardScripts.GetTemplateFromScriptPubKey(network, script);
        }
    }
}

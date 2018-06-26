using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IScriptTemplateFactory
    {
        ScriptTemplate GetTemplateFromScriptPubKey(Network network, Script script);
    }
}

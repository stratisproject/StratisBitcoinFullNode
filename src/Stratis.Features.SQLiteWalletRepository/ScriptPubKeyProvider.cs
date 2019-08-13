using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository
{
    public interface IScriptPubKeyProvider
    {
        Script FromPubKey(PubKey pubKey, string scriptPubKeyType);
    }

    public class ScriptPubKeyProvider : IScriptPubKeyProvider
    {
        public Script FromPubKey(PubKey pubKey, string scriptPubKeyType)
        {
            Script scriptPubKey = null;

            ScriptTemplate scriptTemplate;
            switch (scriptPubKeyType)
            {
                case "P2PK":
                    scriptTemplate = PayToPubkeyTemplate.Instance;
                    scriptPubKey = (scriptTemplate as PayToPubkeyTemplate).GenerateScriptPubKey(pubKey);
                    break;
                case "P2PKH":
                    scriptTemplate = PayToPubkeyHashTemplate.Instance;
                    scriptPubKey = (scriptTemplate as PayToPubkeyHashTemplate).GenerateScriptPubKey(pubKey);
                    break;
                default:
                    return null;
            }

            return scriptPubKey;
        }
    }
}

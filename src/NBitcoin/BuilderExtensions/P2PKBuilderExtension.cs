using System;

namespace NBitcoin.BuilderExtensions
{
    public class P2PKBuilderExtension : BuilderExtension
    {
        public override bool CanCombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            return false;
        }

        public override bool CanDeduceScriptPubKey(Network network, Script scriptSig)
        {
            return false;
        }

        public override bool CanEstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey) != null;
        }

        public override bool CanGenerateScriptSig(Network network, Script scriptPubKey)
        {
            return PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey) != null;
        }

        public override Script CombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            throw new NotImplementedException();
        }

        public override Script DeduceScriptPubKey(Network network, Script scriptSig)
        {
            throw new NotImplementedException();
        }

        public override int EstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return PayToPubkeyTemplate.Instance.GenerateScriptSig(DummySignature).Length;
        }

        public override Script GenerateScriptSig(Network network, Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
        {
            Key key = keyRepo.FindKey(scriptPubKey);
            if(key == null)
                return null;
            TransactionSignature sig = signer.Sign(key);
            return PayToPubkeyTemplate.Instance.GenerateScriptSig(sig);
        }
    }
}

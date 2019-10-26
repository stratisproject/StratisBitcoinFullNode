namespace NBitcoin.BuilderExtensions
{
    public class P2PKHBuilderExtension : BuilderExtension
    {
        public override bool CanCombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            return PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKey);
        }

        public override bool CanDeduceScriptPubKey(Network network, Script scriptSig)
        {
            PayToPubkeyHashScriptSigParameters para = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, scriptSig);
            return para != null && para.PublicKey != null;
        }

        public override bool CanEstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKey);
        }

        public override bool CanGenerateScriptSig(Network network, Script scriptPubKey)
        {
            return PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKey);
        }

        public override Script CombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            PayToPubkeyHashScriptSigParameters aSig = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, a);
            PayToPubkeyHashScriptSigParameters bSig = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, b);
            if(aSig == null)
                return b;
            if(bSig == null)
                return a;
            var merged = new PayToPubkeyHashScriptSigParameters();
            merged.PublicKey = aSig.PublicKey ?? bSig.PublicKey;
            merged.TransactionSignature = aSig.TransactionSignature ?? bSig.TransactionSignature;
            return PayToPubkeyHashTemplate.Instance.GenerateScriptSig(merged);
        }

        public override Script DeduceScriptPubKey(Network network, Script scriptSig)
        {
            PayToPubkeyHashScriptSigParameters p2pkh = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, scriptSig);
            return p2pkh.PublicKey.Hash.ScriptPubKey;
        }

        public override int EstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return PayToPubkeyHashTemplate.Instance.GenerateScriptSig(DummySignature, DummyPubKey).Length;
        }

        public override Script GenerateScriptSig(Network network, Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
        {
            KeyId parameters = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey);
            Key key = keyRepo.FindKey(parameters.ScriptPubKey);
            if(key == null)
                return null;
            TransactionSignature sig = signer.Sign(key);
            return PayToPubkeyHashTemplate.Instance.GenerateScriptSig(sig, key.PubKey);
        }
    }
}

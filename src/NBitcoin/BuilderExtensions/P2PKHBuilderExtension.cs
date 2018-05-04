namespace NBitcoin.BuilderExtensions
{
    public class P2PKHBuilderExtension : BuilderExtension
    {
        public override bool CanCombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            return PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(network, scriptPubKey);
        }

        public override bool CanDeduceScriptPubKey(Network network, Script scriptSig)
        {
            var para = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, scriptSig);
            return para != null && para.PublicKey != null;
        }

        public override bool CanEstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(network, scriptPubKey);
        }

        public override bool CanGenerateScriptSig(Network network, Script scriptPubKey)
        {
            return PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(network, scriptPubKey);
        }

        public override Script CombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            var aSig = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, a);
            var bSig = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, b);
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
            var p2pkh = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(network, scriptSig);
            return p2pkh.PublicKey.Hash.ScriptPubKey;
        }

        public override int EstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return PayToPubkeyHashTemplate.Instance.GenerateScriptSig(DummySignature, DummyPubKey).Length;
        }

        public override Script GenerateScriptSig(Network network, Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
        {
            var parameters = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey);
            var key = keyRepo.FindKey(parameters.ScriptPubKey);
            if(key == null)
                return null;
            var sig = signer.Sign(key);
            return PayToPubkeyHashTemplate.Instance.GenerateScriptSig(sig, key.PubKey);
        }
    }
}

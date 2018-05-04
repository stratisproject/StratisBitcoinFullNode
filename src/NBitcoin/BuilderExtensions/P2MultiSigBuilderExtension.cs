using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.BuilderExtensions
{
    public class P2MultiSigBuilderExtension : BuilderExtension
    {
        public override bool CanCombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            return PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey) != null;
        }

        public override bool CanDeduceScriptPubKey(Network network, Script scriptSig)
        {
            return false;
        }

        public override bool CanEstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey) != null;
        }

        public override bool CanGenerateScriptSig(Network network, Script scriptPubKey)
        {
            return PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey) != null;
        }

        public override Script CombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            var para = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey);
            // Combine all the signatures we've got:
            var aSigs = PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(network, a);
            if(aSigs == null)
                return b;
            var bSigs = PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(network, b);
            if(bSigs == null)
                return a;
            int sigCount = 0;
            TransactionSignature[] sigs = new TransactionSignature[para.PubKeys.Length];
            for(int i = 0; i < para.PubKeys.Length; i++)
            {
                var aSig = i < aSigs.Length ? aSigs[i] : null;
                var bSig = i < bSigs.Length ? bSigs[i] : null;
                var sig = aSig ?? bSig;
                if(sig != null)
                {
                    sigs[i] = sig;
                    sigCount++;
                }
                if(sigCount == para.SignatureCount)
                    break;
            }
            if(sigCount == para.SignatureCount)
                sigs = sigs.Where(s => s != null && s != TransactionSignature.Empty).ToArray();
            return PayToMultiSigTemplate.Instance.GenerateScriptSig(sigs);
        }

        public override Script DeduceScriptPubKey(Network network, Script scriptSig)
        {
            throw new NotImplementedException();
        }

        public override int EstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            var p2mk = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey);
            return PayToMultiSigTemplate.Instance.GenerateScriptSig(Enumerable.Range(0, p2mk.SignatureCount).Select(o => DummySignature).ToArray()).Length;
        }

        public override Script GenerateScriptSig(Network network, Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
        {
            var multiSigParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey);
            TransactionSignature[] signatures = new TransactionSignature[multiSigParams.PubKeys.Length];
            var keys =
                multiSigParams
                .PubKeys
                .Select(p => keyRepo.FindKey(p.ScriptPubKey))
                .ToArray();

            int sigCount = 0;
            for(int i = 0; i < keys.Length; i++)
            {
                if(sigCount == multiSigParams.SignatureCount)
                    break;
                if(keys[i] != null)
                {
                    var sig = signer.Sign(keys[i]);
                    signatures[i] = sig;
                    sigCount++;
                }
            }

            IEnumerable<TransactionSignature> sigs = signatures;
            if(sigCount == multiSigParams.SignatureCount)
            {
                sigs = sigs.Where(s => s != TransactionSignature.Empty && s != null);
            }
            return PayToMultiSigTemplate.Instance.GenerateScriptSig(sigs);
        }
    }
}

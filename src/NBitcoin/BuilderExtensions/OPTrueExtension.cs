using System;

namespace NBitcoin.BuilderExtensions
{
    public class OPTrueExtension : BuilderExtension
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
            return CanGenerateScriptSig(network, scriptPubKey);
        }

        public override bool CanGenerateScriptSig(Network network, Script scriptPubKey)
        {
            return scriptPubKey.Length == 1 && scriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_TRUE;
        }

        public override Script CombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            throw new NotSupportedException();
        }

        public override Script DeduceScriptPubKey(Network network, Script scriptSig)
        {
            throw new NotSupportedException();
        }

        public override int EstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return 1;
        }

        public override Script GenerateScriptSig(Network network, Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
        {
            return Script.Empty;
        }
    }
}

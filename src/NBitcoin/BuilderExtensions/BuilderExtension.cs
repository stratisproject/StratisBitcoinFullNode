﻿using NBitcoin.DataEncoders;

namespace NBitcoin.BuilderExtensions
{
    public interface ISigner
    {
        TransactionSignature Sign(Key key);
    }
    public interface IKeyRepository
    {
        Key FindKey(Script scriptPubKey);
    }

    /// <summary>
    /// Base extension class to derive from for extending the TransactionBuilder
    /// </summary>
    public abstract class BuilderExtension
    {
        public static PubKey DummyPubKey = new PubKey(Encoders.Hex.DecodeData("022c2b9e61169fb1b1f2f3ff15ad52a21745e268d358ba821d36da7d7cd92dee0e"));
        public static TransactionSignature DummySignature = new TransactionSignature(Encoders.Hex.DecodeData("3045022100b9d685584f46554977343009c04b3091e768c23884fa8d2ce2fb59e5290aa45302203b2d49201c7f695f434a597342eb32dfd81137014fcfb3bb5edc7a19c77774d201"));

        public abstract bool CanGenerateScriptSig(Network network, Script scriptPubKey);
        public abstract Script GenerateScriptSig(Network network, Script scriptPubKey, IKeyRepository keyRepo, ISigner signer);

        public abstract Script DeduceScriptPubKey(Network network, Script scriptSig);
        public abstract bool CanDeduceScriptPubKey(Network network, Script scriptSig);

        public abstract bool CanEstimateScriptSigSize(Network network, Script scriptPubKey);
        public abstract int EstimateScriptSigSize(Network network, Script scriptPubKey);

        public abstract bool CanCombineScriptSig(Network network, Script scriptPubKey, Script a, Script b);

        public abstract Script CombineScriptSig(Network network, Script scriptPubKey, Script a, Script b);
    }
}

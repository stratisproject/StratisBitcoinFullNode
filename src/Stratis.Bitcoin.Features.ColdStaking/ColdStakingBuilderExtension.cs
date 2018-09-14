using System;
using NBitcoin;
using NBitcoin.BuilderExtensions;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// Extends the <see cref="TransactionBuilder" /> functionality to play nice with cold staking script.
    /// This is loosely based on the <see cref="P2PKHBuilderExtension" /> with the difference that our
    /// scriptSigs take an additional parameter to identify which public key hash to use (hot or cold).
    /// </summary>
    public class ColdStakingBuilderExtension : BuilderExtension
    {
        // Instructs this object to use either to coldPubKeyHash (when false) or the hotPubKeyHash (when true).
        private readonly bool staking;

        /// <summary>
        /// Constructs an object for use with staking or cold staking withdrawal transactions.
        /// </summary>
        /// <param name="staking">Set to <c>true</c> when staking. Set to <c>false</c> when spending from cold staking addresses.</param>
        public ColdStakingBuilderExtension(bool staking)
        {
            this.staking = staking;
        }

        /// <inheritdoc />
        public override bool CanCombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            return false;
        }

        /// <inheritdoc />
        public override bool CanDeduceScriptPubKey(Network network, Script scriptSig)
        {
            return false;
        }

        /// <inheritdoc />
        public override bool CanEstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            return ColdStakingScriptTemplate.Instance.CheckScriptPubKey(scriptPubKey);
        }

        /// <inheritdoc />
        public override bool CanGenerateScriptSig(Network network, Script scriptPubKey)
        {
            return ColdStakingScriptTemplate.Instance.CheckScriptPubKey(scriptPubKey);
        }

        /// <inheritdoc />
        /// <remarks>Combining is not defined for cold staking scripts.</remarks>
        public override Script CombineScriptSig(Network network, Script scriptPubKey, Script a, Script b)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        /// <remarks>It is not possible to construct the original scriptPubKey from a cold staking scriptSig.</remarks>
        public override Script DeduceScriptPubKey(Network network, Script scriptSig)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override int EstimateScriptSigSize(Network network, Script scriptPubKey)
        {
            // Adds as dummy coldPubKey flag.
            return ColdStakingScriptTemplate.Instance.GenerateScriptSig(DummySignature, false, DummyPubKey).Length;
        }

        /// <inheritdoc />
        public override Script GenerateScriptSig(Network network, Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
        {
            ColdStakingScriptTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey, out KeyId hotKey, out KeyId coldKey);

            // The scriptPubKey will be different depending on whether we are spending or cold staking.
            Key key = keyRepo.FindKey((this.staking ? hotKey : coldKey).ScriptPubKey);
            if (key == null)
                return null;

            TransactionSignature sig = signer.Sign(key);

            return ColdStakingScriptTemplate.Instance.GenerateScriptSig(sig, !this.staking, key.PubKey);
        }
    }
}


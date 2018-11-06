using NBitcoin;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// The scriptSig parameters used for cold staking script.
    /// </summary>
    public class ColdStakingScriptSigParameters
    {
        /// <summary>The signature used with <see cref="OpcodeType.OP_CHECKSIG"/>.</summary>
        public TransactionSignature TransactionSignature { get; set; }

        /// <summary>A flag indicating whether this is coldPubKey.</summary>
        public bool IsColdPublicKey { get; set; }

        /// <summary>This is either the coldPubKey or the hotPubKey.</summary>
        public PubKey PublicKey { get; set; }
    }
}
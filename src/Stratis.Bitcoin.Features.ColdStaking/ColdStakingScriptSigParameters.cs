using NBitcoin;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// The scriptSig parameters used for cold staking script.
    /// </summary>
    public class ColdStakingScriptSigParameters : IDestination
    {
        /// <summary>The signature used with OP_CHECKSIG.</summary>
        public TransactionSignature TransactionSignature { get; set; }

        /// <summary>A flag indicating whether this is coldPubKeyHash.</summary>
        public bool IsColdPublicKey { get; set; }

        /// <summary>This is either the coldPubKeyHash or the hotPubKeyHash.</summary>
        public PubKey PublicKey { get; set; }

        /// <summary>Returns the public key hash.</summary>
        public virtual TxDestination Hash { get { return this.PublicKey.Hash; } }

        /// <summary>Returns the scriptPubKey of the public key hash.</summary>
        public Script ScriptPubKey { get { return this.Hash.ScriptPubKey; } }
    }
}
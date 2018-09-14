using NBitcoin;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    public class ColdStakingScriptSigParameters : IDestination
    {
        public TransactionSignature TransactionSignature { get; set; }
        public bool ColdPublicKey { get; set; }
        public PubKey PublicKey { get; set; }

        public virtual TxDestination Hash { get { return this.PublicKey.Hash; } }

        #region IDestination Members

        public Script ScriptPubKey { get { return this.Hash.ScriptPubKey; } }

        #endregion
    }
}

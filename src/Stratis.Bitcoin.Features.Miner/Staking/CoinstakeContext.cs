using NBitcoin;

namespace Stratis.Bitcoin.Features.Miner.Staking
{
    /// <summary>
    /// Information about coinstake transaction and its private key.
    /// </summary>
    public class CoinstakeContext
    {
        /// <summary>Coinstake transaction being constructed.</summary>
        public Transaction CoinstakeTx { get; set; }

        /// <summary>The previous output being referred to in the coinstake's input.</summary>
        public TxOut KernelTxOut { get; set; }

        /// <summary>If the function succeeds, this is filled with private key for signing the coinstake kernel.</summary>
        public Key Key { get; set; }
    }
}
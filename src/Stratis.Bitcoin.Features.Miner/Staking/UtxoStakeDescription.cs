using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner.Staking
{
    /// <summary>
    /// Information related to UTXO that is required for staking.
    /// </summary>
    public class UtxoStakeDescription
    {
        /// <summary>Block's hash.</summary>
        public uint256 HashBlock { get; set; }

        /// <summary>UTXO that participates in staking. It's a part of <see cref="UtxoSet"/>.</summary>
        public TxOut TxOut { get; set; }

        /// <summary>Information about transaction id and index.</summary>
        public OutPoint OutPoint { get; set; }

        /// <summary>Address of the transaction that has spendable coins for staking.</summary>
        public HdAddress Address { get; set; }

        /// <summary>Selected outputs of a transaction.</summary>
        public UnspentOutputs UtxoSet { get; set; }

        /// <summary>Credentials to wallet that contains the private key for the staking UTXO.</summary>
        public WalletSecret Secret { get; set; }

        /// <summary>Private key that is needed for spending coins associated with the <see cref="Address"/>.</summary>
        public Key Key { get; set; }
    }
}
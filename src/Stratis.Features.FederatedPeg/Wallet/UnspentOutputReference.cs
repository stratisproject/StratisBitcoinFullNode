using NBitcoin;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// Represents an UTXO that keeps a reference to an address.
    /// </summary>
    public class UnspentOutputReference
    {
        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }

        /// <summary>
        /// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
        /// </summary>
        /// <returns>The corresponding <see cref="OutPoint"/>.</returns>
        public OutPoint ToOutPoint()
        {
            return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
        }
    }
}
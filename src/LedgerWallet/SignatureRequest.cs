using NBitcoin;

namespace LedgerWallet
{
    public class SignatureRequest
    {
        public ICoin InputCoin
        {
            get; set;
        }
        public Transaction InputTransaction
        {
            get; set;
        }
        public KeyPath KeyPath
        {
            get; set;
        }
        public PubKey PubKey
        {
            get; set;
        }
        public TransactionSignature Signature
        {
            get;
            set;
        }
    }
}

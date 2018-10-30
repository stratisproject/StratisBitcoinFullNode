using NBitcoin;

namespace Stratis.FederatedPeg.Tests.Utils
{
    public class TestTransactionBuilder
    {
        public Transaction BuildOpReturnTransaction(BitcoinPubKeyAddress receiverAddress, byte[] opReturnBytes, Money amount = null)
        {
            var transaction = new Transaction();
            transaction.AddOutput(new TxOut(amount ?? Money.COIN, receiverAddress));
            transaction.AddOutput(new TxOut(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes))));
            return transaction;
        }

        public Transaction BuildTransaction(BitcoinPubKeyAddress receiverAddress)
        {
            var transaction = new Transaction();
            transaction.AddOutput(new TxOut(Money.COIN, receiverAddress));
            return transaction;
        }
    }
}
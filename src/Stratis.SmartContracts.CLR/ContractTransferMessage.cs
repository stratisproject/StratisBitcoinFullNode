using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Represents an internal contract transfer message. Occurs when a contract generates a funds transfer using
    /// its <see cref="SmartContract.Transfer"/> method.  If the recipient is another contract, the recipient's
    /// receive handler will be invoked. If the recipient is a P2PKH, a UTXO will be generated and included in the
    /// transaction.
    /// </summary>
    public class ContractTransferMessage : InternalCallMessage
    {
        public ContractTransferMessage(uint160 to, uint160 from, ulong amount, RuntimeObserver.Gas gasLimit) 
            : base(to, from, amount, gasLimit, MethodCall.Receive())
        {
        }
    }
}
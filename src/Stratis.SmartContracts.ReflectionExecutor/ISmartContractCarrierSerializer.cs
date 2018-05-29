using NBitcoin;

namespace Stratis.SmartContracts.ReflectionExecutor
{
    public interface ISmartContractCarrierSerializer
    {
       /// <summary>
       /// In the future this could use an ITransaction in case we use a different transaction structure. Non UTXO maybe
       /// </summary>
       ISmartContractCarrier Deserialize(Transaction transaction);
    }
}

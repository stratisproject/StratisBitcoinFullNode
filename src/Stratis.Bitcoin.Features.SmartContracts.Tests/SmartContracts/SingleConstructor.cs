using Stratis.SmartContracts;

    public class SingleConstructor : SmartContract
    {
        public SingleConstructor(ISmartContractState smartContractState)
            : base(smartContractState)
        {
        }
    }
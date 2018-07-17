using Stratis.SmartContracts;

    public class MultipleConstructor : SmartContract
    {
        public MultipleConstructor(ISmartContractState smartContractState)
            : base(smartContractState)
        {
        }

        public MultipleConstructor(ISmartContractState smartContractState, uint param)
            : base(smartContractState)
        {
        }
}
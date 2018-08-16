using Stratis.SmartContracts;

namespace MultipleFiles
{
    public class MultipleFiles1 : SmartContract
    {
        protected MultipleFiles1(ISmartContractState smartContractState) : base(smartContractState)
        {
            var test = Create<MultipleFiles2>();
        }
    }
}

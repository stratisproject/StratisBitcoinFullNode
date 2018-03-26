using Stratis.SmartContracts;

namespace $safeprojectname$
{
    public class Contract : SmartContract
    {
        public Contract(ISmartContractState smartContractState)
            : base(smartContractState)
        {
        }

        public string Test()
        {
            return "Test";
        }
    }
}

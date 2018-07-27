using System.Threading.Tasks;
using Stratis.SmartContracts;

public class AsyncGenericTask : SmartContract
{
    public AsyncGenericTask(ISmartContractState smartContractState)
        : base(smartContractState)
    {
    }

    public AsyncGenericTask(ISmartContractState smartContractState, uint param)
        : base(smartContractState)
    {
    }

    public async Task<bool> Test()
    {
        return await Task.FromResult(true);
    }
}
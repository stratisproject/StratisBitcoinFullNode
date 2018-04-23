using System.Threading.Tasks;
using Stratis.SmartContracts;

public class AsyncTask : SmartContract
{
    public AsyncTask(ISmartContractState smartContractState)
        : base(smartContractState)
    {
    }

    public AsyncTask(ISmartContractState smartContractState, uint param)
        : base(smartContractState)
    {
    }

    public async Task Test()
    {
    }
}
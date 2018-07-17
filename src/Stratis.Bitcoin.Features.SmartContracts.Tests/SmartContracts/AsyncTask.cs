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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task Test()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
    }
}
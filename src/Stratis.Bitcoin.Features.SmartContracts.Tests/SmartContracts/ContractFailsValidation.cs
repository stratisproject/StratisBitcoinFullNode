using System.Threading.Tasks;
using Stratis.SmartContracts;

public sealed class ContractFailsValidation : SmartContract
{
    public ContractFailsValidation(ISmartContractState state)
        : base(state)
    {
    }

    public void TestMethod()
    {
        var task = new Task(() => { });
        task.Start();
    }
}
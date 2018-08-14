using Stratis.SmartContracts;
using Stratis.SmartContracts.ByteHelper;

public class AllowedAssemblies : SmartContract
{
    public AllowedAssemblies(ISmartContractState state) : base(state)
    {
    }

    public void Test()
    {
        ByteConverter.ToBool(new byte());
    }
}
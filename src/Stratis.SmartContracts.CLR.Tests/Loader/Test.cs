namespace Stratis.SmartContracts.CLR.Tests.Loader
{
    [Deploy]
    public class Test : SmartContract
    {
        public Test(ISmartContractState state)
            : base(state)
        { }
    }

    public class NotDeployedType : SmartContract
    {
        public NotDeployedType(ISmartContractState state)
            :base(state)
        { }
    }
}

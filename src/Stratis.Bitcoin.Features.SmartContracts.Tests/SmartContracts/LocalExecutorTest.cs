using Stratis.SmartContracts;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.SmartContracts
{
    public class LocalExecutorTest : SmartContract
    {
        public LocalExecutorTest(ISmartContractState smartContractState)
            : base(smartContractState)
        {
        }

        public string ReturnsResult()
        {
            return "Result";
        }

        public void GenerateInternalTransfers(Address address1, ulong amount1, Address address2, ulong amount2)
        {
            Transfer(address1, amount1);
            Transfer(address2, amount2);
        }

        public void ConsumeKnownGas()
        {
        }

        public void ConsumeAllGas()
        {
            while (true) { }
        }

        public void GenerateLog()
        {
            Log(new TestLog { Name = "Test" });
        }

        public struct TestLog
        {
            public string Name;
        }
    }
}

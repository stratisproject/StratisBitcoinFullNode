namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    public class ContractLogHolder : IContractLogger
    {
        public void Log<T>(ISmartContractState smartContractState, T toLog)
        {
            throw new System.NotImplementedException();
        }
    }
}

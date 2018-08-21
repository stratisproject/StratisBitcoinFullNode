namespace Stratis.SmartContracts
{
    public interface IContractLogger
    {
        void Log<T>(ISmartContractState smartContractState, T toLog);
    }
}

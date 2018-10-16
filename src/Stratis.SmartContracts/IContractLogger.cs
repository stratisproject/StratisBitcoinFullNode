namespace Stratis.SmartContracts
{
    public interface IContractLogger
    {
        /// <summary>
        /// Adds an event to be logged as occuring during execution of this contract.
        /// </summary>
        void Log<T>(ISmartContractState smartContractState, T toLog) where T : struct;
    }
}

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ICreateData : IBaseContractTransactionData
    {
        /// <summary>The contract code that will be executed.</summary>
        byte[] ContractExecutionCode { get; }
    }
}
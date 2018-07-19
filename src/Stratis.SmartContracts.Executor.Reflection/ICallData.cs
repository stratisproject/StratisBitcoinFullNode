namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ICallData : IBaseContractTransactionData
    {
        /// <summary>The method name of the contract that will be executed.</summary>
        string MethodName { get; }
    }
}
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ICallData : IBaseContractTransactionData
    {
        /// <summary>
        /// The address of the contract being called
        /// </summary>
        uint160 ContractAddress { get; }

        /// <summary>The method name of the contract that will be executed.</summary>
        string MethodName { get; }
    }
}
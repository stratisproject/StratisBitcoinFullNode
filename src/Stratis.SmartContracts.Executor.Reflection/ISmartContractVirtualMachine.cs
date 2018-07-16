using CSharpFunctionalExtensions;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Serializer for call data sent with a transaction
    /// </summary>
    public interface ICallDataSerializer
    {
        Result<CallData> Deserialize(byte[] callData);
    }

    public interface ISmartContractVirtualMachine
    {
        VmExecutionResult Create(byte[] contractCode,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter, IPersistentState persistentState, IContractStateRepository repository);

        VmExecutionResult ExecuteMethod(byte[] contractCode,
            string methodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter, IPersistentState persistentState, IContractStateRepository repository);
    }
}

using Mono.Cecil;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractGasInjector
    {
        void AddGasCalculationToContract(TypeDefinition contractType, TypeDefinition baseType);
    }
}
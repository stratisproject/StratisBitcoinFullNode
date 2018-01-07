using Stratis.SmartContracts.ContractValidation.Result;

namespace Stratis.SmartContracts.ContractValidation
{
    public interface ISmartContractValidator
    {
        SmartContractValidationResult Validate(SmartContractDecompilation decompilation);
    }
}


namespace Stratis.SmartContracts.ContractValidation
{
    public interface ISmartContractValidator
    {
        SmartContractValidationResult Validate(SmartContractDecompilation decompilation);
    }
}

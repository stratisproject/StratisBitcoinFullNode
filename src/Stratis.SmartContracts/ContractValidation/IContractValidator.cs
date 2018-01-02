using Stratis.SmartContracts.ContractValidation.Result;

namespace Stratis.SmartContracts.ContractValidation
{
    internal interface IContractValidator
    {
        ContractValidationResult Validate();
    }
}

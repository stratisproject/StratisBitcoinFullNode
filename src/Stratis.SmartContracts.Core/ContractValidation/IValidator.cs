using Stratis.SmartContracts.Core.Compilation;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public interface IValidator
    {
        /// <summary>
        /// Validate all user defined methods in the contract.
        /// <para>
        /// All methods with an empty body will be ignored.
        /// </para>
        /// </summary>
        SmartContractValidationResult Validate(SmartContractDecompilation decompilation);
    }
}
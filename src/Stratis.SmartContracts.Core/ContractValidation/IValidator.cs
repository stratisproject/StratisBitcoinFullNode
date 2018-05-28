using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;

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
        ValidationResult Validate(SmartContractDecompilation decompilation);
    }
}
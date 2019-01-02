using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    public interface ISmartContractValidator
    {
        /// <summary>
        /// Validate all user defined methods in the contract.
        /// <para>
        /// All methods with an empty body will be ignored.
        /// </para>
        /// </summary>
        SmartContractValidationResult Validate(ModuleDefinition moduleDefinition);
    }
}
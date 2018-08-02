using System.Linq;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates the format of a Smart Contract <see cref="SmartContractDecompilation"/>
    /// </summary>
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            ValidationPolicy policy = FormatPolicy.Default;

            var validator = new ModulePolicyValidator(policy);

            var results = validator.Validate(decompilation.ModuleDefinition).ToList();

            return new SmartContractValidationResult(results);
        }
    }
}
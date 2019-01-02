using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    public sealed class SmartContractValidator : ISmartContractValidator
    {
        public SmartContractValidationResult Validate(ModuleDefinition moduleDefinition)
        {
            var policy = ValidationPolicy.FromExisting(new[] { FormatPolicy.Default, DeterminismPolicy.Default });
            var validator = new ModulePolicyValidator(policy);

            var results = validator.Validate(moduleDefinition).ToList();
            return new SmartContractValidationResult(results);
        }
    }
}
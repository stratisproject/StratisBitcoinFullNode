using System.Linq;
namespace Stratis.SmartContracts.Core.Validation
{
    public sealed class SmartContractValidator : ISmartContractValidator
    {
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var policy = ValidationPolicy.FromExisting(new[] { FormatPolicy.Default, DeterminismPolicy.Default });
            var validator = new ModulePolicyValidator(policy);

            var results = validator.Validate(decompilation.ModuleDefinition).ToList();
            return new SmartContractValidationResult(results);
        }
    }
}
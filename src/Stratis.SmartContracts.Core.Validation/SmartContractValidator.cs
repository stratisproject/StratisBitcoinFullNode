using System.Linq;
namespace Stratis.SmartContracts.Core.Validation
{
    public sealed class SmartContractValidator : ISmartContractValidator
    {
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var formatPolicy = FormatPolicyFactory.CreatePolicy();
            var determinismPolicy = new DeterminismPolicyFactory().CreatePolicy();

            var policy = ValidationPolicy.FromExisting(new[] {formatPolicy, determinismPolicy});
            var validator = new ModulePolicyValidator(policy);

            var results = validator.Validate(decompilation.ModuleDefinition).ToList();
            return new SmartContractValidationResult(results);
        }
    }
}
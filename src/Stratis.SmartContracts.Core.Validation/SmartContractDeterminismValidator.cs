namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Checks for non-deterministic properties inside smart contracts by validating them at the bytecode level.
    /// </summary>
    public class SmartContractDeterminismValidator : ISmartContractValidator
    {
        /// <inheritdoc/>
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var policy = DeterminismPolicy.Default;
            var validator = new ModulePolicyValidator(policy);
            var validationResults = validator.Validate(decompilation.ModuleDefinition);

            return new SmartContractValidationResult(validationResults);
        }
    }
}
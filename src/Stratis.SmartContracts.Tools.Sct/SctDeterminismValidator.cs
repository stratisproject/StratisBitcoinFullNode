using Stratis.SmartContracts.Core.Validation;

namespace Stratis.SmartContracts.Tools.Sct
{
    public class SctDeterminismValidator 
    {
        private static readonly ISmartContractValidator Validator = new SmartContractDeterminismValidator(); 
        
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            return Validator.Validate(decompilation.ModuleDefinition);
        }
    }
}
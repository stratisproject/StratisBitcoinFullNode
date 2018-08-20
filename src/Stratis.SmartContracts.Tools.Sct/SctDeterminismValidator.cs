using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;

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
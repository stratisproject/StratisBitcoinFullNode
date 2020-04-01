using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.CLR;

namespace Stratis.SmartContracts.Tools.Sct
{
    public class SctDeterminismValidator 
    {
        private static readonly ISmartContractValidator Validator = new SmartContractDeterminismValidator(); 
        
        public SmartContractValidationResult Validate(IContractModuleDefinition moduleDefinition)
        {
            return Validator.Validate(moduleDefinition.ModuleDefinition);
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validate that parameters to methods used in Smart Contracts are of types that are currently supported in the
    /// <see cref="MethodParameterSerializer"/>
    /// </summary>
    public class MethodParamValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Invalid Method Param Type";

        // See SmartContractCarrierDataType
        public static readonly IEnumerable<string> AllowedTypes = new[]
        {
            typeof(bool).FullName,
            typeof(byte).FullName,
            typeof(byte[]).FullName,
            typeof(char).FullName,
            typeof(sbyte).FullName,
            typeof(int).FullName,
            typeof(string).FullName,
            typeof(uint).FullName,
            typeof(ulong).FullName,
            typeof(Address).FullName
        };

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition methodDef)
        {
            if (!methodDef.HasParameters)
                return Enumerable.Empty<SmartContractValidationError>();

            // Constructor is allowed to have other params
            if (methodDef.IsConstructor)
                return Enumerable.Empty<SmartContractValidationError>();

            return methodDef.Parameters
                .Where(param => !AllowedTypes.Contains(param.ParameterType.FullName))
                .Select(paramDef =>
                    new SmartContractValidationError(
                        paramDef.Name,
                        methodDef.FullName,
                        ErrorType,
                        $"{methodDef.FullName} is invalid [{ErrorType} {paramDef.ParameterType.FullName}]"
                    ));
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validate that a <see cref="Mono.Cecil.MethodDefinition"/> only has parameters of types that are currently supported in the
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
            typeof(long).FullName,
            typeof(Address).FullName
        };

        public IEnumerable<FormatValidationError> Validate(MethodDefinition methodDef)
        {
            if (!methodDef.HasParameters)
                return Enumerable.Empty<FormatValidationError>();

            // Constructor is allowed to have other params
            if (methodDef.IsConstructor)
                return Enumerable.Empty<FormatValidationError>();

            return methodDef.Parameters
                .Where(param => !AllowedTypes.Contains(param.ParameterType.FullName))
                .Select(paramDef =>
                    new FormatValidationError(
                        paramDef.Name,
                        methodDef.FullName,
                        ErrorType,
                        $"{methodDef.FullName} is invalid [{ErrorType} {paramDef.ParameterType.FullName}]"
                    ));
        }
    }
}
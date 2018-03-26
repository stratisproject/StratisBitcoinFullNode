using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Serialization;

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
        public static readonly IEnumerable<string> AllowedTypes = new []
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

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            if (!method.HasParameters)
                return Enumerable.Empty<SmartContractValidationError>();

            // Constructor is allowed to have other params
            if (method.IsConstructor)
                return Enumerable.Empty<SmartContractValidationError>();

            return method.Parameters
                .Where(param => !AllowedTypes.Contains(param.ParameterType.FullName))
                .Select(m => 
                    new SmartContractValidationError(
                        m.Name,
                        method.FullName,
                        ErrorType,
                        $"{method.FullName} is invalid [{ErrorType} {m.ParameterType.FullName}]"
                    ));            
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
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

        public IEnumerable<ValidationResult> Validate(MethodDefinition methodDef)
        {
            if (!methodDef.HasParameters)
                return Enumerable.Empty<MethodDefinitionValidationResult>();


            bool IsValidParamForThisMethod(ParameterDefinition p) => !IsValidParam(methodDef, p);

            return methodDef.Parameters
                .Where(IsValidParamForThisMethod)
                .Select(paramDef => new MethodParamValidationResult(methodDef, paramDef));
        }

        /// <summary>
        /// Checks that constructor methods contains an allowed parameter. Allowed parameters are <see cref="ISmartContractState"/>
        /// and the types defined in <see cref="AllowedTypes"/>
        /// </summary>
        public static bool IsValidParam(MethodDefinition methodDefinition, ParameterDefinition param)
        {
            if (methodDefinition.IsConstructor && ParameterIsSmartContractState(param))
            {
                return true;
            }

            return AllowedTypes.Contains(param.ParameterType.FullName);
        }

        private static bool ParameterIsSmartContractState(ParameterDefinition param)
        {
            return param.ParameterType.FullName == typeof(ISmartContractState).FullName;
        }

        public class MethodParamValidationResult : MethodDefinitionValidationResult
        {
            public MethodParamValidationResult(MethodDefinition method, ParameterDefinition param) 
                : base(method.FullName,
                    ErrorType,
                    $"{method.FullName} is invalid [{ErrorType} {param.ParameterType.FullName}]")
            {
            }
        }
    }
}
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
        public static readonly string ErrorType = "Invalid Method Param";

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

            var results = new List<ValidationResult>();

            foreach (var param in methodDef.Parameters)
            {
                (var valid, var message) = IsValidParam(methodDef, param);

                if (!valid)
                {
                    results.Add(new MethodParamValidationResult(message));
                }
            }

            return results;
        }

        /// <summary>
        /// Checks that constructor methods contains an allowed parameter. Allowed parameters are <see cref="ISmartContractState"/>
        /// and the types defined in <see cref="AllowedTypes"/>
        /// </summary>
        public static (bool, string) IsValidParam(MethodDefinition methodDefinition, ParameterDefinition param)
        {
            if (param.IsOptional)
            {
                return (false, $"{methodDefinition.FullName} is invalid [{param.Name} is disallowed optional parameter]");
            }

            if (methodDefinition.IsConstructor && ParameterIsSmartContractState(param))
            {
                return (true, null);
            }

            var allowedType = AllowedTypes.Contains(param.ParameterType.FullName);

            return allowedType
                ? (true, null)
                : (false,
                    $"{methodDefinition.FullName} is invalid [{param.Name} is disallowed parameter Type {param.ParameterType.FullName}]");
        }

        private static bool ParameterIsSmartContractState(ParameterDefinition param)
        {
            return param.ParameterType.FullName == typeof(ISmartContractState).FullName;
        }

        public class MethodParamValidationResult : MethodDefinitionValidationResult
        {
            public MethodParamValidationResult(string message) : base(message)
            {
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Stratis.ModuleValidation.Net;
using Stratis.ModuleValidation.Net.Format;
using Stratis.SmartContracts.Core.Validation.Validators.Module;
using Stratis.SmartContracts.Core.Validation.Validators.Type;

namespace Stratis.SmartContracts.Core.Validation
{
    public class FormatPolicyFactory
    {
        public static Func<FieldDefinition, bool> DisallowedField = field => !(field.DeclaringType.IsNested && field.DeclaringType.IsValueType) && !field.HasConstant;
        public static Func<MethodDefinition, bool> HasTryCatch = method => method.Body.HasExceptionHandlers;

        public static Func<TypeDefinition, bool> TypeHasMethods = type => type.HasMethods;
        public static Func<TypeDefinition, bool> TypeHasNestedTypes = type => type.HasNestedTypes;
        public static Func<TypeDefinition, bool> NestedTypesAreValueTypes = type => type.HasNestedTypes && !type.NestedTypes.All(n => n.IsValueType);

        public static Func<ModuleDefinition, bool> SingleTypeValidator = module => module.Types.Count(x => !(x.FullName.Contains("<Module>") || x.FullName.Contains("<PrivateImplementationDetails>"))) > 1;
        public static Func<TypeDefinition, bool> NamespaceValidator = type => type != null && type.Namespace != "";
        
        // System.Runtime forwards to mscorlib, so we can only get its Assembly by name
        // ref. https://github.com/dotnet/corefx/issues/11601
        private static readonly Assembly Runtime = Assembly.Load("System.Runtime");
        private static readonly Assembly Core = typeof(object).Assembly;

        /// <summary>
        /// The set of Assemblies that a <see cref="SmartContract"/> is required to reference
        /// </summary>
        public static HashSet<Assembly> AllowedAssemblies = new HashSet<Assembly> {
            Runtime,
            Core,
            typeof(SmartContract).Assembly,
            typeof(Enumerable).Assembly
        };

        public ValidationPolicy CreatePolicy()
        {
            return new ValidationPolicy()
                .ModuleDefValidator(new AssemblyReferenceValidator(AllowedAssemblies))
                .ModuleDefValidator(
                    SingleTypeValidator,
                    m => new ModuleDefinitionValidationResult("Only the compilation of a single class is allowed.")
                    )
                .TypeDefValidator(new StaticConstructorValidator(), NestedTypePolicy.Ignore)
                .TypeDefValidator(new NamespaceValidator())
                .TypeDefValidator(new SingleConstructorValidator(), NestedTypePolicy.Ignore)
                .TypeDefValidator(new ConstructorParamValidator(), NestedTypePolicy.Ignore)
                .TypeDefValidator(new InheritsSmartContractValidator(), NestedTypePolicy.Ignore
                )
                .NestedTypeDefValidator(
                    TypeHasMethods,
                    t => new TypeHasMethodsValidator.TypeHasMethodsValidationResult(t))
                .NestedTypeDefValidator(
                    TypeHasNestedTypes,
                    t => new TypeHasNestedTypesValidator.TypeHasNestedTypesValidationResult(t))
                .NestedTypeDefValidator(
                    NestedTypesAreValueTypes,
                    t => new NestedTypesAreValueTypesValidator.NestedTypeIsValueTypeValidationResult("Nested Types must be Value Types"))
                .FieldDefValidator(
                    DisallowedField,
                    (t, f) => new FieldDefinitionValidator.FieldDefinitionValidationResult(t, f))
                .MethodDefValidator(
                    HasTryCatch,
                    (t, m) => new TryCatchValidator.TryCatchValidationResult(m))
                .MethodParamValidator((m, p) => !MethodParamValidator.IsValidParam(m, p),
                    (t, m, p) => new MethodParamValidator.MethodParamValidationResult(m, p));
        }
    }

    /// <summary>
    /// Validates the format of a Smart Contract <see cref="SmartContractDecompilation"/>
    /// </summary>
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var policy = new FormatPolicyFactory().CreatePolicy();

            var validator = new ModuleDefValidator(policy);

            var results = validator.Validate(decompilation.ModuleDefinition).ToList();  

            return new SmartContractValidationResult(results);
        }  
    }
}

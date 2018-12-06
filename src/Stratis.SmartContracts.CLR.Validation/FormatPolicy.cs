using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.CLR.Validation.Validators.Method;
using Stratis.SmartContracts.CLR.Validation.Validators.Module;
using Stratis.SmartContracts.CLR.Validation.Validators.Type;
using Stratis.SmartContracts.Standards;

namespace Stratis.SmartContracts.CLR.Validation
{
    public static class FormatPolicy
    {
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
            typeof(Enumerable).Assembly,
            typeof(IStandardToken).Assembly
        };

        public static ValidationPolicy Default = new ValidationPolicy()
            .ModuleDefValidator(new AssemblyReferenceValidator(AllowedAssemblies))
            .ModuleDefValidator(new ContractToDeployValidator())
            .TypeDefValidator(new StaticConstructorValidator(), NestedTypePolicy.Ignore)
            .TypeDefValidator(new GenericTypeValidator())
            .TypeDefValidator(new NamespaceValidator())
            .TypeDefValidator(new SingleConstructorValidator(), NestedTypePolicy.Ignore)
            .TypeDefValidator(new ConstructorParamValidator(), NestedTypePolicy.Ignore)
            .TypeDefValidator(new InheritsSmartContractValidator(), NestedTypePolicy.Ignore)
            .TypeDefValidator(new FieldDefinitionValidator(), NestedTypePolicy.Ignore)
            .NestedTypeDefValidator(new TypeHasMethodsValidator())
            .NestedTypeDefValidator(new TypeHasNestedTypesValidator())
            .NestedTypeDefValidator(new NestedTypesAreValueTypesValidator())
            .MethodDefValidator(new TryCatchValidator())
            .MethodDefValidator(new MethodParamValidator())
            .MethodDefValidator(new GenericMethodValidator())
            .InstructionValidator(new MultiDimensionalArrayValidator())
            .InstructionValidator(new NewObjValidator());
    }   
}
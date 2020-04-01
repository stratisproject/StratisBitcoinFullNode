using Stratis.SmartContracts.CLR.Validation.Validators.Method;
using Stratis.SmartContracts.CLR.Validation.Validators.Module;
using Stratis.SmartContracts.CLR.Validation.Validators.Type;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Expanded format policy that removes assembly reference validation.
    /// </summary>
    public static class ExpandedFormatPolicy
    {
        public static ValidationPolicy Default = new ValidationPolicy()
            .ModuleDefValidator(new ModuleReferenceValidator())
            .ModuleDefValidator(new ContractToDeployValidator())
            .TypeDefValidator(new NamespaceValidator())
            .TypeDefValidator(new StaticConstructorValidator(), t => t.IsContractType())
            .TypeDefValidator(new GenericTypeValidator(), t => t.IsContractType())
            .TypeDefValidator(new SingleConstructorValidator(), t => t.IsContractType())
            .TypeDefValidator(new ConstructorParamValidator(), t => t.IsContractType())
            .TypeDefValidator(new InheritsSmartContractValidator(), t => t.IsContractType())
            .TypeDefValidator(new FieldDefinitionValidator(), t => t.IsContractType())
            .NestedTypeDefValidator(new TypeHasMethodsValidator())
            .NestedTypeDefValidator(new TypeHasNestedTypesValidator())
            .NestedTypeDefValidator(new NestedTypeIsValueTypeValidator())
            .MethodDefValidator(new TryCatchValidator())
            .MethodDefValidator(new MethodParamValidator())
            .MethodDefValidator(new GenericMethodValidator())
            .MethodDefValidator(new PInvokeValidator())
            .InstructionValidator(new MultiDimensionalArrayValidator())
            .InstructionValidator(new NewObjValidator());
    }
}
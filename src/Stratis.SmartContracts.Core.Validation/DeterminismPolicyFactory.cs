using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Stratis.ModuleValidation.Net.Determinism;
using Stratis.ModuleValidation.Net.Format;
using Stratis.SmartContracts.Core.Validation.Policy;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Defines a policy used to evaluate a TypeDefinition for uses of non-deterministic Types
    /// </summary>
    public class DeterminismPolicyFactory
    {
        public static Func<TypeDefinition, bool> HasFinalizer = type => type.Methods.Any(m => m.Name == "Finalize");

        public ValidationPolicy CreatePolicy()
        {
            WhitelistPolicy whitelistPolicy = this.CreateWhitelistPolicy();

            return new ValidationPolicy()
                .WhitelistValidator(whitelistPolicy)
                .TypeDefValidator(new FinalizerValidator())
                .InstructionValidator(new FloatValidator());
        }

        public WhitelistPolicy CreateWhitelistPolicy()
        {
            return new WhitelistPolicy()
                .Namespace(nameof(System), AccessPolicy.Denied, SystemPolicy)
                .Namespace(typeof(RuntimeHelpers).Namespace, AccessPolicy.Denied, CompilerServicesPolicy)
                .Namespace(typeof(SmartContract).Namespace, AccessPolicy.Allowed, SmartContractsPolicy);
        }

        private static void SystemPolicy(NamespacePolicy policy)
        {
            foreach (Type type in Primitives.Types)
            {
                policy
                    .Type(type, AccessPolicy.Allowed);
            }

            policy
                .Type(typeof(void).Name, AccessPolicy.Allowed)
                .Type(typeof(object).Name, AccessPolicy.Denied, 
                    m => m.Member(nameof(ToString), AccessPolicy.Allowed)
                          .Constructor(AccessPolicy.Allowed));
        }

        private static void CompilerServicesPolicy(NamespacePolicy compilerServices)
        {
            compilerServices
                .Type(nameof(IteratorStateMachineAttribute), AccessPolicy.Allowed)
                .Type(nameof(RuntimeHelpers), AccessPolicy.Denied,
                    t => t.Member(nameof(RuntimeHelpers.InitializeArray), AccessPolicy.Allowed)
                );
        }

        private static void SmartContractsPolicy(NamespacePolicy policy)
        {
            policy
                .Type(typeof(SmartContract), AccessPolicy.Allowed,
                    type => type.Member(nameof(SmartContract.SpendGas), AccessPolicy.Denied));
        }
    }
}
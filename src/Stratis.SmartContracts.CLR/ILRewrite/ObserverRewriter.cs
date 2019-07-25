using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    /// <summary>
    /// Injects a new type with reference to an <see cref="Observer"/> into a module which can be used to track runtime metrics.
    /// </summary>
    public class ObserverRewriter : IILRewriter
    {
        private const string InjectedNamespace = "<Stratis>";
        private const string InjectedTypeName = "<RuntimeObserverInstance>";
        private const string InjectedPropertyName = "Instance";

        /// <summary>
        /// The individual rewriters to be applied to each method, which use the injected type.
        /// </summary>
        private static readonly List<IObserverMethodRewriter> methodRewriters = new List<IObserverMethodRewriter>
        {
            new GasInjectorRewriter(),
            new MemoryLimitRewriter()
        };


        private readonly Observer observerToInject;

        public ObserverRewriter(Observer observer)
        {
            this.observerToInject = observer;
        }

        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            Guid id = Guid.NewGuid();

            (FieldDefinition observerInstanceField, TypeDefinition observerType) = this.GetObserverInstance(module, id);
            var observer = new ObserverReferences(observerInstanceField, module);

            foreach (TypeDefinition type in module.GetTypes())
            {
                this.RewriteType(type, observer);
            }

            ObserverInstances.Set(id, this.observerToInject);

            module.Types.Add(observerType);

            return module;
        }

        /// <summary>
        /// Inserts a static type into the module which gives access to an instance of <see cref="Observer"/>.
        /// Because this is injected per module, it counts as a separate type and will not be a shared static.
        /// </summary>
        private (FieldDefinition, TypeDefinition) GetObserverInstance(ModuleDefinition module, Guid id)
        {
            // Add new type that can't be instantiated
            var instanceType = new TypeDefinition(
                InjectedNamespace, InjectedTypeName,
                TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic,
                module.ImportReference(typeof(object))
            );

            // Add a field - an instance of our Observer!
            var instanceField = new FieldDefinition(
                InjectedPropertyName,
                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly,
                module.ImportReference(typeof(Observer))
            );
            instanceType.Fields.Add(instanceField);

            // When this type is created, retrieve the Observer from our global static dictionary so it can be used.
            var constructor = new MethodDefinition(
                ".cctor", MethodAttributes.Private | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Static,
                module.ImportReference(typeof(void))
            );
            MethodReference getGuardInstance = module.ImportReference(typeof(ObserverInstances).GetMethod(nameof(ObserverInstances.Get)));
            ILProcessor il = constructor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldstr, id.ToString());
            il.Emit(OpCodes.Call, getGuardInstance);
            il.Emit(OpCodes.Stsfld, instanceField);
            il.Emit(OpCodes.Ret);
            instanceType.Methods.Add(constructor);

            return (instanceField, instanceType);
        }

        private void RewriteType(TypeDefinition type, ObserverReferences observer)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                this.RewriteMethod(method, observer);
            }
        }

        /// <summary>
        /// Makes the <see cref="Observer"/> available to the given method as a variable and then
        /// applies all of the individual rewriters to the method.
        /// </summary>
        private void RewriteMethod(MethodDefinition methodDefinition, ObserverReferences observer)
        {
            if (!methodDefinition.HasBody || methodDefinition.Body.Instructions.Count == 0)
                return; // don't inject on method without a Body 

            // Inject observer instance to method.
            ILProcessor il = methodDefinition.Body.GetILProcessor();
            var observerVariable = new VariableDefinition(observer.InstanceField.FieldType);
            il.Body.Variables.Add(observerVariable);
            Instruction start = methodDefinition.Body.Instructions[0];
            il.InsertBefore(start, il.Create(OpCodes.Ldsfld, observer.InstanceField));
            il.InsertBefore(start, il.CreateStlocBest(observerVariable));

            var context = new ObserverRewriterContext(observer, observerVariable);

            foreach(IObserverMethodRewriter rewriter in methodRewriters)
            {
                rewriter.Rewrite(methodDefinition, il, context);
            }
        }
    }
}

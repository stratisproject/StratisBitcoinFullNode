using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    public class ObserverReplacerRewriter : IILRewriter
    {
        private readonly Observer observerToInject;

        public ObserverReplacerRewriter(Observer observerToInject)
        {
            this.observerToInject = observerToInject;
        }

        /// <summary>
        /// Replaces the observer in the given module.
        /// 
        /// Assumes that the incoming module has already been rewritten to include an observer. Putting a standard module in here will throw an exception.
        /// </summary>
        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            // Get the new ID we can swap in so that our new observer is accessed.
            Guid id = Guid.NewGuid();

            // Get the existing place that ID is stored in the constructor. Note exception will be thrown if this doesn't exist.
            TypeDefinition instanceAccessor = module.Types.First(x => x.Name == ObserverRewriter.InjectedTypeName);
            MethodDefinition constructor = instanceAccessor.Methods.First(x => x.Name == ObserverRewriter.ConstructorName);

            // The ID should be loaded in the very first instruction.
            if (constructor.Body.Instructions[0].OpCode != OpCodes.Ldstr)
                throw new ArgumentException(@"Incoming module is not in the correct format. Expected Ldstr at index 1.", nameof(module));

            // Replace the old ID load instruction with our new one.
            ILProcessor il = constructor.Body.GetILProcessor();
            il.Replace(constructor.Body.Instructions[0], il.Create(OpCodes.Ldstr, id.ToString()));
            
            ObserverInstances.Set(id, this.observerToInject);

            return module;
        }
    }
}

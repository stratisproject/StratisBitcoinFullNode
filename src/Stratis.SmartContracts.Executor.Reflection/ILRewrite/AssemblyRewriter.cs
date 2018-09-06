using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using RuntimeObserver;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public class AssemblyRewriter
    {
        public static Guid Rewrite(Stream source, Stream target)
        {
            if (target == source) // For Cecil.
                throw new ArgumentException("Target stream must be different from source stream.", nameof(target));

            var assembly = AssemblyDefinition.ReadAssembly(source);
            Guid token = Rewrite(assembly);
            assembly.Write(target);
            return token;
        }

        // Does whole assembly but we probably only want to do module...

        public static Guid Rewrite(AssemblyDefinition assembly)
        {
            var id = Guid.NewGuid();
            foreach (ModuleDefinition module in assembly.Modules)
            {
                RewriteModule(module, id);
            }
            return id;
        }

        // May be able to remove 'id' here

        public static void RewriteModule(ModuleDefinition module, Guid id)
        {
            FieldDefinition observerInstanceField = GetObserverInstance(module, id);
            var guard = new ObserverReferences(observerInstanceField, module);

            foreach (TypeDefinition type in module.Types)
            {
                RewriteType(type, guard);
            }
        }

        private static FieldDefinition GetObserverInstance(ModuleDefinition module, Guid id)
        {
            var instanceType = new TypeDefinition(
                "<Stratis>", "<RuntimeObserverInstance>",
                TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic,
                module.ImportReference(typeof(object))
            );
            var instanceField = new FieldDefinition(
                "Instance",
                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly,
                module.ImportReference(typeof(Observer))
            );
            instanceType.Fields.Add(instanceField);

            var constructor = new MethodDefinition(
                ".cctor", MethodAttributes.Private | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Static,
                module.ImportReference(typeof(void))
            );
            MethodReference getGuardInstance = module.ImportReference(typeof(ObserverInstance).GetMethod(nameof(ObserverInstance.Get)));
            ILProcessor il = constructor.Body.GetILProcessor();
            il.Emit(OpCodes.Call, getGuardInstance);
            il.Emit(OpCodes.Stsfld, instanceField);
            il.Emit(OpCodes.Ret);
            instanceType.Methods.Add(constructor);

            module.Types.Add(instanceType);
            return instanceField;
        }

        private static void RewriteType(TypeDefinition type, ObserverReferences guard)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                RewriteMethod(method, guard);
            }
        }

        private static void RewriteMethod(MethodDefinition method, ObserverReferences guard)
        {
            if (method.DeclaringType == guard.InstanceField.DeclaringType)
                return;

            if (!method.HasBody)
                return;

            if (method.Body.Instructions.Count == 0)
                return; // weird, but happens with 'extern'

            var isStaticConstructor = method.Name == ".cctor" && method.IsStatic && method.IsRuntimeSpecialName;
            var il = method.Body.GetILProcessor();
            var guardVariable = new VariableDefinition(guard.InstanceField.FieldType);
            il.Body.Variables.Add(guardVariable);

            var instructions = il.Body.Instructions;
            var start = instructions[0];
            var skipFirst = 4;
            il.InsertBefore(start, il.Create(OpCodes.Ldsfld, guard.InstanceField));
            il.InsertBefore(start, il.Create(OpCodes.Dup));
            il.InsertBefore(start, il.CreateStlocBest(guardVariable));
            il.InsertBefore(start, il.Create(OpCodes.Call, isStaticConstructor ? guard.GuardEnterStaticConstructorMethod : guard.GuardEnterMethod));

            for (var i = skipFirst; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                var memberRule = validator.ValidateInstructionAndGetPolicy(instruction, method);
                var code = instruction.OpCode.Code;
                if (code == Code.Newarr)
                {
                    il.InsertBeforeAndRetargetJumps(instruction, il.CreateLdlocBest(guardVariable));
                    il.InsertBefore(instruction, il.CreateCall(guard.FlowThroughGuardCountIntPtrMethod));
                    i += 2;
                    continue;
                }

                if (memberRule != null && memberRule.Rewriters.Count > 0)
                {
                    var instructionCountBefore = instructions.Count;
                    var rewritten = false;
                    var context = new MemberRewriterContext(il, guardVariable, guard);
                    foreach (var rewriter in memberRule.InternalRewriters)
                    {
                        rewritten = rewriter.Rewrite(instruction, context) || rewritten;
                    }
                    if (rewritten)
                    {
                        i += instructions.Count - instructionCountBefore;
                        continue;
                    }
                }

                if (isStaticConstructor && instruction.OpCode.Code == Code.Ret)
                {
                    il.InsertBeforeAndRetargetJumps(instruction, il.CreateLdlocBest(guardVariable));
                    il.InsertBefore(instruction, il.CreateCall(guard.GuardExitStaticConstructorMethod));
                    i += 2;
                    continue;
                }

                if (!ShouldInsertJumpGuardBefore(instruction))
                    continue;

                il.InsertBeforeAndRetargetJumps(instruction, il.CreateLdlocBest(guardVariable));
                il.InsertBefore(instruction, il.Create(OpCodes.Call, guard.GuardJumpMethod));
                i += 2;
            }

            il.CorrectAllAfterChanges();
        }
    }

    public static class CecilExtensions
    {
        public static Instruction CreateStlocBest(this ILProcessor il, VariableDefinition variable)
        {
            switch (variable.Index)
            {
                case 0: return il.Create(OpCodes.Stloc_0);
                case 1: return il.Create(OpCodes.Stloc_1);
                case 2: return il.Create(OpCodes.Stloc_2);
                case 3: return il.Create(OpCodes.Stloc_3);
                default:
                    if (IsSByte(variable.Index))
                        return il.Create(OpCodes.Stloc_S, variable);
                    return il.Create(OpCodes.Stloc, variable);
            }
        }
    }
}

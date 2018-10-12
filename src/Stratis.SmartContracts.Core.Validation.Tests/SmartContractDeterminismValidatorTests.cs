using System.IO;
using System.Linq;
using Stratis.ModuleValidation.Net;
using Stratis.ModuleValidation.Net.Format;
using Stratis.SmartContracts.Core.Validation.Validators;
using Stratis.SmartContracts.Core.Validation.Validators.Module;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.SmartContracts.Core.Validation.Tests
{
    public class SmartContractValidatorTests
    {
        public IContractModuleDefinition CompileFileToModuleDef(FileInfo file)
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile(file.FullName);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            return decomp;
        }

        public IContractModuleDefinition CompileToModuleDef(string source)
        {
            ContractCompilationResult compilationResult = ContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            return decomp;
        }

        [Fact]
        public void SmartContractValidator_StandardContract_Auction()
        {
            // Validate a standard auction contract
            IContractModuleDefinition decompilation = CompileFileToModuleDef(new FileInfo("Contracts/Auction.cs"));

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContractValidator_StandardContract_Token()
        {
            // Validate a standard auction contract
            IContractModuleDefinition decompilation = CompileFileToModuleDef(new FileInfo("Contracts/Token.cs"));

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContractValidator_Should_Allow_Enum()
        {
            const string source = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public enum A { X, Y, Z }

    public Test(ISmartContractState state)
        : base(state) { }
}";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Empty(result.Errors);
        }

        [Fact]
        public void SmartContractValidator_Should_Allow_New_NestedType()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A {}

                                                public Test(ISmartContractState state)
                                                    : base(state) { }

                                                public void B() { var test = new A(); }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Empty(result.Errors);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_New()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) { }

                                                public void B() { 
                                                    var s = new string('c', 1);  
                                                    var t = new System.Runtime.CompilerServices.TaskAwaiter();
                                                }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Contains(result.Errors, e => e is WhitelistValidator.WhitelistValidationResult);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_GetType()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public void A()
                                                {
                                                    var z = this.GetType();
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";
            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Contains(result.Errors, e => e is WhitelistValidator.WhitelistValidationResult);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Try_Catch_Empty()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public void A()
                                                {
                                                    try {} catch {}
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";
            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Single(result.Errors);
            Assert.IsType<TryCatchValidator.TryCatchValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Try_Catch_All()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public void A()
                                                {
                                                    try {} catch (Exception) {}
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";
            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Single(result.Errors);
            Assert.IsType<TryCatchValidator.TryCatchValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Try_Catch_Named()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public void A()
                                                {
                                                    try {} catch (Exception e) {}
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Single(result.Errors);
            Assert.IsType<TryCatchValidator.TryCatchValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Try_Catch_Filtered()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public void A()
                                                {
                                                    var abc = ""Test"";
                                                    try { } catch (Exception) when (abc == ""Test"") { }
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.Contains(result.Errors, e => e is TryCatchValidator.TryCatchValidationResult);
        }

        [Fact]
        public void SmartContractValidator_Should_Allow_Nesting_With_Fields()
        {
            // This test checks that the NestedTypeValidator fails for multiple levels of nesting
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A
                                                {
                                                    public string B;
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContractValidator_Should_Allow_One_Level_Of_Nesting()
        {
            // This test checks that the NestedTypeValidator fails for multiple levels of nesting
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A
                                                {
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Two_Levels_Of_Nesting()
        {
            // This test checks that the NestedTypeValidator fails for multiple levels of nesting
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A
                                                {
                                                    public struct B {
                                                    }
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<TypeHasNestedTypesValidator.TypeHasNestedTypesValidationResult>(result.Errors.Single());
            Assert.Equal("Test/A", result.Errors.Single().SubjectName);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Three_Levels_Of_Nesting()
        {
            // This test checks that the NestedTypeValidator fails for multiple levels of nesting
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A
                                                {
                                                    public struct B {
                                                        public struct C {
                                                        }
                                                    }
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e is TypeHasNestedTypesValidator.TypeHasNestedTypesValidationResult);
            Assert.Contains(result.Errors, e => e.SubjectName == "Test/A");
            Assert.Contains(result.Errors, e => e.SubjectName == "Test/A/B");
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Nesting_With_Methods()
        {
            // This test checks that the NestedTypeValidator fails for multiple levels of nesting
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A
                                                {
                                                    public void B() {}
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<TypeHasMethodsValidator.TypeHasMethodsValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Nesting_With_Constructor()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A
                                                {
                                                    public A(int abc) { }
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<TypeHasMethodsValidator.TypeHasMethodsValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Nesting_With_Static_Constructor()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public struct A
                                                {
                                                    static A() { }
                                                }

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<TypeHasMethodsValidator.TypeHasMethodsValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Fields()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public string A;
                                                private int B;
                                                private int C = 123;

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Equal(3, result.Errors.Count());
            Assert.True(result.Errors.All(e => e is FieldDefinitionValidator.FieldDefinitionValidationResult));
        }

        [Fact]
        public void SmartContractValidator_Should_Allow_Consts()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {                                              
                                                public const string Item = ""Test"";
                                                public Test(ISmartContractState state)
                                                    : base(state) { var abc = Item.Length; }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Static_Constructors()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {                                              
                                                static Test() {}                                                

                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e is StaticConstructorValidator.StaticConstructorValidationResult);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Finalizer()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {                                              
                                                public Test(ISmartContractState state)
                                                    : base(state) { }

                                                ~Test() {}
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            // Use of finalizer override triggers multiple errors because of the way that it's compiled
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e is FinalizerValidator.FinalizerValidationResult);
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Finalizer_Method()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {                                              
                                                public Test(ISmartContractState state)
                                                    : base(state) { }

                                                void Finalize() {}
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<FinalizerValidator.FinalizerValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Allow_SingleContract()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {                                              
                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void SmartContractValidator_Allow_MultipleContracts_OneDeploy()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;
                                            
                                            [Deploy]
                                            public class Test : SmartContract
                                            {                                              
                                                public Test(ISmartContractState state)
                                                    : base(state) { }

                                            }

                                            public class Test2 : SmartContract
                                            {                                              
                                                public Test2(ISmartContractState state)
                                                    : base(state) { }

                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void SmartContractValidator_Dont_Allow_MultipleContracts_NoDeploy()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {                                              
                                                public Test(ISmartContractState state)
                                                    : base(state) { }

                                            }

                                            public class Test2 : SmartContract
                                            {                                              
                                                public Test2(ISmartContractState state)
                                                    : base(state) { }

                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<ContractToDeployValidator.ContractToDeployValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Dont_Allow_MultipleContracts_MultipleDeploy()
        {
            const string source = @"using System;
                                            using Stratis.SmartContracts;
                                            
                                            [Deploy]
                                            public class Test : SmartContract
                                            {                                              
                                                public Test(ISmartContractState state)
                                                    : base(state) { }
                                            }

                                            [Deploy]
                                            public class Test2 : SmartContract
                                            {                                              
                                                public Test2(ISmartContractState state)
                                                    : base(state) { }
                                            }";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsType<ContractToDeployValidator.ContractToDeployValidationResult>(result.Errors.Single());
        }

        [Fact]
        public void SmartContractValidator_Should_Not_Allow_Optional_Params()
        {
            const string source = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state)
        : base(state) { }

    public void Optional(int optionalParam = 1) {
    }
}";

            IContractModuleDefinition decompilation = CompileToModuleDef(source);

            SmartContractValidationResult result = new SmartContractValidator().Validate(decompilation.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e is MethodParamValidator.MethodParamValidationResult);
        }
    }
}
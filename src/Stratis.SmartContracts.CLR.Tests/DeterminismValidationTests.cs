using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.CLR.Validation.Validators;
using Stratis.SmartContracts.CLR.Validation.Validators.Instruction;
using Stratis.SmartContracts.CLR.Validation.Validators.Method;
using Stratis.SmartContracts.CLR.Validation.Validators.Type;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    /// <summary>
    /// In the long run, it would be great if there is a way for us to run through all possible types and methods
    /// in the system namespace and see how they are evaluated. Depending on what we find out we may have to change our algo.
    /// </summary>
    public class DeterminismValidationTest
    {
        private const string TestString = @"using System;
                                            using Stratis.SmartContracts;
                                            [References]

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}

                                                public void TestMethod()
                                                {
                                                    [CodeToExecute]
                                                }
                                            }";

        private const string ReplaceReferencesString = "[References]";
        private const string ReplaceCodeString = "[CodeToExecute]";

        private readonly ISmartContractValidator validator = new SmartContractValidator();

        #region Action

        [Fact]
        public void Validate_Determinism_ActionAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new Action(() =>
            {
                int insideAction = 5 + 6;
            });
            test();").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_ValidateActionDisallowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new Action(() =>
            {
                var insideAction = DateTime.Now;
            });
            test();").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Activator

        [Fact]
        public void Validate_Determinism_ActivatorNotAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"Address ts = System.Activator.CreateInstance<Address>();").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsAssignableFrom<WhitelistValidator.WhitelistValidationResult>(result.Errors.Single());
        }

        #endregion

        #region Anonymous Classes 

        [Fact]
        public void Validate_Determinism_AnonymousClassFloats()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new
            {
                Prop1 = 6.8
            };").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = ContractCompiler.Compile(adjustedSource).Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateAnonymousClassesDisallowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new
            {
                Test = ""Stratis""
            };").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = ContractCompiler.Compile(adjustedSource).Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region AppDomain

        [Fact]
        public void Validate_Determinism_AppDomain()
        {
            // AppDomain should not be available
            // We do not compile contracts with a reference to System.Runtime.Extensions
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = AppDomain.CurrentDomain; var test2 = test.Id;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.False(compilationResult.Success);
        }

        #endregion

        #region Async

        [Fact]
        public void Validate_Determinism_Async()
        {
            string adjustedSource = @"using System;
                                            using Stratis.SmartContracts;
                                            using System.Threading.Tasks;

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}

                                            public async void Bid()
                                            {
                                                await Task.Run(job);
                                            }

                                            public async Task job()
                                            {
                                                int w = 9;
                                            }
                                            }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);

            Assert.False(result.IsValid);
        }

        #endregion

        #region Arrays

        /*
         * The compiler handles arrays differently when they're initialised with less
         * than 2 or more than 2 elements.
         * 
         * In the case where it's more than 2 items it adds a <PrivateImplementationDetails> type to the module.
         * 
         * We test for both cases below.
         */

        [Fact]
        public void Validate_Determinism_Passes_ArrayConstruction_MoreThan2()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new int[]{2,2,3};").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = ContractCompiler.Compile(adjustedSource).Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Passes_ArrayConstruction_LessThan2()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new int[]{10167};").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = ContractCompiler.Compile(adjustedSource).Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Passes_Array_AllowedMembers()
        {
            string code = @"
            var test = new int[25];
            var test2 = new int[25];
            test[0] = 123;
            int ret = test[0];
            int len = test.Length;
            Array.Resize(ref test, 50);
            Array.Copy(test, test2, len);";

            string adjustedSource = TestString.Replace(ReplaceCodeString,code).Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = ContractCompiler.Compile(adjustedSource).Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Fails_Array_Clone()
        {
            string code = @"
            var test = new int[25];
            var ret = test.Clone();";

            string adjustedSource = TestString.Replace(ReplaceCodeString, code).Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = ContractCompiler.Compile(adjustedSource).Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Fails_MultiDimensional_Arrays()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new int[50,50];").Replace(ReplaceReferencesString, "");
            byte[] assemblyBytes = ContractCompiler.Compile(adjustedSource).Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region BitConverter

        [Fact]
        public void Validate_Determinism_BitConverter()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = BitConverter.IsLittleEndian;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.IsAssignableFrom<WhitelistValidator.WhitelistValidationResult>(result.Errors.Single());
        }

        #endregion

        #region DateTime

        [Fact]
        public void Validate_Determinism_DateTimeNow()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = DateTime.Now;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_DateTimeToday()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = DateTime.Today;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Decimal and Floats

        [Fact]
        public void Validate_Determinism_Floats()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "float test = (float) 3.5; test = test + 1;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e is FloatValidator.FloatValidationResult);
        }

        [Fact]
        public void Validate_Determinism_Double()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "double test = 3.5;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource, OptimizationLevel.Debug);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e is FloatValidator.FloatValidationResult);
        }

        [Fact]
        public void Validate_Determinism_Decimal()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "decimal test = (decimal) 3.5; test = test / 2;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.True(result.Errors.All(e => e is WhitelistValidator.WhitelistValidationResult));
            Assert.True(result.Errors.All(e => e.Message.Contains("Decimal")));
        }

        [Fact]
        public void Validate_Determinism_DecimalParse()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString,
                "decimal largerAmount = decimal.Parse(\"1\");")
                .Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.True(result.Errors.All(e => e is WhitelistValidator.WhitelistValidationResult));
            Assert.True(result.Errors.All(e => e.Message.Contains("Decimal")));
        }

        #endregion

        #region Dynamic

        [Fact]
        public void Validate_Determinism_DynamicTypeAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "dynamic test = 56; test = \"aString\"; ").Replace(ReplaceReferencesString, "");
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        #endregion

        #region Environment

        [Fact]
        public void Validate_Determinism_Environment()
        {
            // Environment should not be available
            // We do not compile contracts with a reference to System.Runtime.Extensions
            string adjustedSource = TestString.Replace(ReplaceCodeString, "int test = Environment.TickCount;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.False(compilationResult.Success);
        }

        #endregion

        #region Exceptions

        [Fact]
        public void Validate_Determinism_Exceptions()
        {
            // Note that NotFiniteNumberException is commented out - it causes an issue as it deals with floats
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new AccessViolationException();
                var test2 = new AggregateException();
                var test4 = new ApplicationException();
                var test5 = new ArgumentException();
                var test6 = new ArgumentNullException();
                var test7 = new ArgumentOutOfRangeException();
                var test8 = new ArithmeticException();
                var test9 = new ArrayTypeMismatchException();
                var test10 = new BadImageFormatException();
                var test13 = new DataMisalignedException();
                var test14 = new DivideByZeroException();
                var test15 = new DllNotFoundException();
                var test16 = new DuplicateWaitObjectException();
                var test17 = new EntryPointNotFoundException();
                var test19 = new FieldAccessException();
                var test20 = new FormatException();
                var test21 = new IndexOutOfRangeException();
                var test22 = new InsufficientExecutionStackException();
                var test23 = new InsufficientMemoryException();
                var test24 = new InvalidCastException();
                var test25 = new InvalidOperationException();
                var test26 = new InvalidProgramException();
                var test27 = new InvalidTimeZoneException();
                var test28 = new MemberAccessException();
                var test29 = new MethodAccessException();
                var test30 = new MissingFieldException();
                var test31 = new MissingMemberException();
                var test32 = new MissingMethodException();
                var test33 = new MulticastNotSupportedException();
                var test35 = new NotImplementedException();
                var test36 = new NotSupportedException();
                var test37 = new NullReferenceException();
                var test38 = new ObjectDisposedException(""test"");
                var test39 = new OperationCanceledException();
                var test40 = new OutOfMemoryException();
                var test41 = new OverflowException();
                var test42 = new PlatformNotSupportedException();
                var test43 = new RankException();
                var test44 = new StackOverflowException();
                var test45 = new SystemException();
                var test46 = new TimeoutException();
                var test47 = new TimeZoneNotFoundException();
                var test48 = new TypeAccessException();
                var test49 = new TypeInitializationException(""test"", new Exception());
                var test50 = new TypeLoadException();
                var test51 = new TypeUnloadedException();
                var test52 = new UnauthorizedAccessException();"
            ).Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion Exceptions

        #region GetType

        [Fact]
        public void Validate_Determinism_GetType()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var type = GetType();").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Generics

        [Fact]
        public void Validate_Determinism_GenericMethod()
        {
            string adjustedSource = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}         

                                                public T TestMethod<T>(int param)
                                                {
                                                    return default(T);
                                                }
                                            }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.True(result.Errors.First() is GenericMethodValidator.GenericMethodValidationResult);
        }

        [Fact]
        public void Validate_Determinism_GenericClass()
        {
            string adjustedSource = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test<T> : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}         

                                                public T TestMethod(int param)
                                                {
                                                    return default(T);
                                                }
                                            }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.True(result.Errors.First() is GenericTypeValidator.GenericTypeValidationResult);
        }

        #endregion

        #region GetHashCode

        [Fact]
        public void Validate_Determinism_GetHashCode()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "int hashCode = GetHashCode();").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_GetHashCode_Overridden()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"
                }
                    public override int GetHashCode()
                {
                return base.GetHashCode();
            ").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }    

        #endregion

        #region Globalisation

        [Fact]
        public void Validate_Determinism_Globalisation()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString,
                "var r = System.Globalization.CultureInfo.CurrentCulture;")
                .Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region IntPtr

        [Fact]
        public void Validate_Determinism_IntPtr_Fails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = new IntPtr(0);").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Inheritance

        // It's not necessarily a requirement that contract inheritance isn't allowed in the long run,
        // but this test allows us to see the currently expected functionality and track changes.

        [Fact]
        public void Validate_Determinism_Inheritance_Fails()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Inheritance.cs");
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region KnownBadMethodCall

        [Fact]
        public void Validate_Determinism_KnownBadMethodCall()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var floor = System.Math.Floor(12D);").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Method Paramaters

        [Fact]
        public void Validate_Determinism_AllowedMethodParams()
        {
            string adjustedSource = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}

                                                public void Bool(bool param)
                                                {
                                                }

                                                public void Byte(byte param)
                                                {
                                                }

                                                public void ByteArray(byte[] param)
                                                {
                                                }

                                                public void Char(char param)
                                                {
                                                }

                                                public void String(string param)
                                                {
                                                }                                           

                                                public void Int32(int param)
                                                {
                                                }

                                                public void UInt32(uint param)
                                                {
                                                }

                                                public void UInt64(ulong param)
                                                {
                                                }

                                                public void Int64(long param)
                                                {
                                                }

                                                public void Address1(Address param)
                                                {
                                                }
                                            }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_DisallowedMethodParams()
        {
            string adjustedSource = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}         

                                                public void DateTime1(DateTime param)
                                                {
                                                }

                                                public void F(float param)
                                                {
                                                }
                                            }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.Equal(2, result.Errors.Count());
            Assert.True(result.Errors.All(e => e is MethodParamValidator.MethodParamValidationResult));
            Assert.Contains(result.Errors, e => e.Message.Contains("System.DateTime"));
            Assert.Contains(result.Errors, e => e.Message.Contains("System.Single"));
        }

        [Fact]
        public void Validate_Determinism_MethodParams_Private()
        {
            string adjustedSource = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}         

                                                private void PrivateStruct(TestStruct test)
                                                {
                                                }

                                                private void PrivateIntArray(int[] test)
                                                {
                                                }
                                                
                                                public void PublicIntArray(int[] test)
                                                {
                                                }

                                                public void PublicStruct(TestStruct test)
                                                {
                                                }

                                                internal void InternalStruct(TestStruct test)
                                                {
                                                }

                                                public struct TestStruct
                                                {
                                                    public int TestInt;
                                                }
                                            }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource, OptimizationLevel.Debug);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
            Assert.Equal(2, result.Errors.Count());
            Assert.Contains("PublicIntArray", result.Errors.ElementAt(0).Message);
            Assert.Contains("PublicStruct", result.Errors.ElementAt(1).Message);
        }

        #endregion

        #region Nullable

        [Fact]
        public void Validate_Determinism_Nullable_Fails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "int? test = null;").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource, OptimizationLevel.Debug);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Recursion

        [Fact]
        public void Validate_Determinism_Recursion()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class RecursionTest : SmartContract
                    {
                        public RecursionTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void Bid()
                        {
                            Job(5);
                        }

                        public void Job(int index)
                        {
                            if (index > 0)
                                Job(index - 1);
                        }
                    }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(compilationResult.Compilation).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        #endregion

        #region SimpleContract

        [Fact]
        public void Validate_Determinism_SimpleContract()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Token.cs");
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        #endregion

        #region TaskAwaiter

        [Fact]
        public void Validate_Determinism_TaskAwaiter()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString,
                    "var r = new System.Runtime.CompilerServices.TaskAwaiter();")
                .Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource, OptimizationLevel.Debug);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        #endregion

        #region TryCatch

        [Fact]
        public void Validate_Determinism_TryCatch()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TryCatch.cs");
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }
        

        #endregion

        [Fact(Skip = "This test fails as it involves compilation of multiple classes, which is a format validation issue, not a determinism one")]
        public void Validate_Determinism_ExtensionMethods()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "\"testString\".Test();").Replace(ReplaceReferencesString, "");

            adjustedSource += @"public static class Extension
                {
                    public static void Test(this string str)
                    {
                        var test = DateTime.Now;
                    }
                }";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_StringIteration()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString,            
                @"int randomNumber = 0;
                  foreach (byte c in ""Abcdefgh"")
                  {
                    randomNumber += c;
                  }").Replace(ReplaceReferencesString, "");

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_ByteArray_Conversion()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ByteArrayConversion.cs");
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;
            SmartContractValidationResult result = this.validator.Validate(moduleDefinition.ModuleDefinition);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_TwoTypes_Valid()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}
}


public class Test2 {
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_TwoTypes_Invalid()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}
}


public class Test2 {
    public Test2() {
        var dt = DateTime.Now;
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Validate_Determinism_ForEach()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}

    public int Sum() 
    {
        var summation = 0;

        foreach(var i in new [] { 1,2,3,4,5,6,7,8,9,10})
        {
            summation += 1;
        }

        return summation;
    }

    public string SumStr() 
    {
        var summation = """";
        var strings = new [] { ""1"",""2"",""3"",""4"",""5"",""6"",""7"",""8"",""9"",""10""};
        foreach (var i in strings)
        {
            summation += i;
        }

        return summation;
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Generator()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
using System.Collections.Generic;

public class Test : SmartContract
{
    public Test(ISmartContractState state) : base(state) {}

    public IEnumerable<int> Sum() 
    {
        var summation = 0;

        foreach(var i in new [] { 1,2,3,4,5,6,7,8,9,10})
        {
            summation += 1;
        }

        yield return summation;
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_PublicPartial_Class()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
public partial class Test : SmartContract
{
    public Test(ISmartContractState state)
        : base(state)
    {
    }

    public void Method1() {}
}

public partial class Test : SmartContract
{
    public void Method2() {}
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            var contractModule = new ContractModuleDefinition(decomp.ModuleDefinition, new MemoryStream());
            Assert.True(result.IsValid);
            Assert.Contains(contractModule.ContractType.Methods, m => m.Name == "Method1");
            Assert.Contains(contractModule.ContractType.Methods, m => m.Name == "Method2");
        }

        [Fact]
        public void Validate_PublicPartial_Struct()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
public class Test : SmartContract
{
    public Test(ISmartContractState state)
        : base(state)
    {
        var s = new ImageLedgerEntry();
    }
}

    public partial struct ImageLedgerEntry  
    {
        private string _imageName;
        private byte[] _imageContent;
    }

    public partial struct ImageLedgerEntry  
    {
        private int _imageVersion;
    }
";

            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
            var fields = decomp.ModuleDefinition.Types.First(t => t.Name == "ImageLedgerEntry").Fields;
            Assert.Contains(fields, m => m.Name == "_imageVersion");
            Assert.Contains(fields, m => m.Name == "_imageName");
            Assert.Contains(fields, m => m.Name == "_imageContent");
        }

        [Fact]
        public void Validate_StaticField()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
public class Test : SmartContract
{
    static string AStaticString = ""Test"";

    public Test(ISmartContractState state)
        : base(state)
    {
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Validate_NestedStaticField()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
public class Test : SmartContract
{
    public Test(ISmartContractState state)
        : base(state)
    {
    }

    public struct Nested
    {
            static string AStaticString = ""Test"";
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Validate_StaticMethod()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
public class Test : SmartContract
{
    static string AStaticMethod() {
        return ""Test"";
    }
    

    public Test(ISmartContractState state)
        : base(state)
    {
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_StaticGenericMethod()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
public class Test : SmartContract
{
    static string AStaticGenericMethod<T>() {
        return ""Test"";
    }
    

    public Test(ISmartContractState state)
        : base(state)
    {
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Validate_StaticMethodParams()
        {
            var adjustedSource = @"
using System;
using Stratis.SmartContracts;
public class Test : SmartContract
{
    static string AStaticMethod(ISmartContractState disallowedParam) {
        return ""Test"";
    }
    

    public Test(ISmartContractState state)
        : base(state)
    {
    }
}
";
            ContractCompilationResult compilationResult = ContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            IContractModuleDefinition decomp = ContractDecompiler.GetModuleDefinition(assemblyBytes).Value;

            var result = this.validator.Validate(decomp.ModuleDefinition);

            Assert.False(result.IsValid);
            Assert.True(result.Errors.All(e => e is MethodParamValidator.MethodParamValidationResult));
            Assert.Contains(result.Errors, e => e.Message.Contains("ISmartContractState"));
        }
    }
}
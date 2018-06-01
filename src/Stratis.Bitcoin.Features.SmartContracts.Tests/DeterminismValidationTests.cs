
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
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

        private readonly SmartContractDeterminismValidator validator = new SmartContractDeterminismValidator();

        #region Action

        [Fact]
        public void Validate_Determinism_ActionAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new Action(() =>
            {
                int insideAction = 5 + 6;
            });
            test();").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_ValidateActionDisallowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new Action(() =>
            {
                var insideAction = DateTime.Now;
            });
            test();").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Activator

        [Fact]
        public void Validate_Determinism_ActivatorNotAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"Address ts = System.Activator.CreateInstance<Address>();").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);

            Assert.False(result.IsValid);
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

            byte[] assemblyBytes = SmartContractCompiler.Compile(adjustedSource).Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateAnonymousClassesDisallowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new
            {
                Test = ""Stratis""
            };").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = SmartContractCompiler.Compile(adjustedSource).Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);

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

            byte[] assemblyBytes = SmartContractCompiler.Compile(adjustedSource).Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Passes_ArrayConstruction_LessThan2()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new int[]{10167};").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = SmartContractCompiler.Compile(adjustedSource).Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }

        #endregion

        #region BitConverter

        [Fact]
        public void Validate_Determinism_BitConverter()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = BitConverter.IsLittleEndian;").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region DateTime

        [Fact]
        public void Validate_Determinism_DateTimeNow()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = DateTime.Now;").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_DateTimeToday()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = DateTime.Today;").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Decimal and Floats

        [Fact]
        public void Validate_Determinism_Floats()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "float test = (float) 3.5; test = test + 1;").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Double()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "double test = 3.5;").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_Decimal()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "decimal test = (decimal) 3.5; test = test / 2;").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Determinism_DecimalParse()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString,
                "decimal largerAmount = decimal.Parse(\"1\");")
                .Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Dynamic

        [Fact]
        public void Validate_Determinism_DynamicTypeAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "dynamic test = 56; test = \"aString\"; ").Replace(ReplaceReferencesString, "");
            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }

        #endregion Exceptions

        [Fact]
        public void Validate_Determinism_GetType()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var type = GetType();").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }

        #region GetHashCode

        [Fact]
        public void Validate_Determinism_GetHashCode()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "int hashCode = GetHashCode();").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region KnownBadMethodCall

        [Fact]
        public void Validate_Determinism_KnownBadMethodCall()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var floor = System.Math.Floor(12D);").Replace(ReplaceReferencesString, "");

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
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

                                                public void SByte(sbyte param)
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Message.Contains("System.DateTime"));
            Assert.Contains(result.Errors, e => e.Message.Contains("System.Single"));
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.True(result.IsValid);
        }

        #endregion

        #region SimpleContract

        [Fact]
        public void Validate_Determinism_SimpleContract()
        {
            var adjustedSource = @"
                using System;
                using Stratis.SmartContracts;

                public class Token : SmartContract
                {
                    public Token(ISmartContractState state): base(state) 
                    {
                        Owner = Message.Sender;
                        Balances = PersistentState.GetUInt64Mapping(""Balances"");
                    }

                    public Address Owner
                    {
                        get
                        {
                            return PersistentState.GetAddress(""Owner"");
                        }
                        private set
                        {
                            PersistentState.SetAddress(""Owner"", value);
                        }
                    }

                    public ISmartContractMapping<ulong> Balances { get; }

                    public bool Mint(Address receiver, ulong amount)
                    {
                        if (Message.Sender != Owner)
                            throw new Exception(""Sender of this message is not the owner. "" + Owner.ToString() +"" vs "" + Message.Sender.ToString());

                        amount = amount + Block.Number;
                        Balances[receiver.ToString()] += amount;
                        return true;
                    }

                    public bool Send(Address receiver, ulong amount)
                    {
                        if (Balances.Get(Message.Sender.ToString()) < amount)
                            throw new Exception(""Sender doesn't have high enough balance"");

                        Balances[receiver.ToString()] += amount;
                        Balances[Message.Sender.ToString()] -= amount;
                        return true;
                    }

                    public void GasTest()
                    {
                        ulong test = 1;
                        while (true)
                        {
                            test++;
                            test--;
                        }
                    }
                }
            ";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);

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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region TryCatch

        [Fact]
        public void Validate_Determinism_TryCatch()
        {
            SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TryCatch.cs");
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }
        

        #endregion

        [Fact]
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
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

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(adjustedSource);
            Assert.True(compilationResult.Success);

            byte[] assemblyBytes = compilationResult.Compilation;
            SmartContractDecompilation decomp = SmartContractDecompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }
    }
}
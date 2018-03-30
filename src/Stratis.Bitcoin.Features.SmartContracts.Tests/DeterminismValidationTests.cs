using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Util;
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

        private readonly SmartContractDecompiler decompiler = new SmartContractDecompiler();

        private readonly SmartContractDeterminismValidator validator = new SmartContractDeterminismValidator();
        // Try to keep all of these in alphabetical order

        #region Action

        [Fact]
        public void ValidateActionAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new Action(() =>
            {
                int insideAction = 5 + 6;
            });
            test();").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);

            SmartContractDecompilation decomp = decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = validator.Validate(decomp);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateActionDisallowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new Action(() =>
            {
                int insideAction = 5 + Environment.TickCount;
            });
            test();").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Anonymous Classes 

        [Fact]
        public void ValidateAnonymousClassFloatsDisallowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new
            {
                Prop1 = 6.8
            };").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
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

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region AppDomain

        [Fact]
        public void ValidateAppDomainFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = AppDomain.CurrentDomain;
                var test2 = test.Id;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region BitConverter

        [Fact]
        public void ValidateBitConverterFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = BitConverter.IsLittleEndian;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region DateTime

        [Fact]
        public void ValidateDateTimeNowFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = DateTime.Now;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateDateTimeTodayFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = DateTime.Today;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Decimal and Floats

        [Fact]
        public void ValidateFloatFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "float test = (float) 3.5; test = test + 1;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateDoubleFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "double test = 3.5;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateDecimalFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "decimal test = (decimal) 3.5; test = test / 2;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Dynamic

        [Fact]
        public void ValidateDynamicTypeAllowed()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "dynamic test = 56; test = \"aString\"; ")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }

        #endregion

        #region Environment

        [Fact]
        public void ValidateEnvironmentFails()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "int test = Environment.TickCount;")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region Exceptions

        [Fact]
        public void AllExceptions()
        {
            // Note that NotFiniteNumberException is commented out - it causes an issue as it deals with floats
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var test = new AccessViolationException();
                var test2 = new AggregateException();
                var test3 = new AppDomainUnloadedException();
                var test4 = new ApplicationException();
                var test5 = new ArgumentException();
                var test6 = new ArgumentNullException();
                var test7 = new ArgumentOutOfRangeException();
                var test8 = new ArithmeticException();
                var test9 = new ArrayTypeMismatchException();
                var test10 = new BadImageFormatException();
                var test11 = new CannotUnloadAppDomainException();
                var test12 = new ContextMarshalException();
                var test13 = new DataMisalignedException();
                var test14 = new DivideByZeroException();
                var test15 = new DllNotFoundException();
                var test16 = new DuplicateWaitObjectException();
                var test17 = new EntryPointNotFoundException();
                var test18 = new ExecutionEngineException();
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
                //var test34 = new NotFiniteNumberException();
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

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }

        #endregion Exceptions

        #region GetHashCode

        [Fact]
        public void ValidateGetHashCode()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "int hashCode = GetHashCode();")
                .Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }
        #endregion

        #region KnownBadMethodCall

        [Fact]
        public void KnownBadMethodCall()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, @"var floor = System.Math.Floor(12D);").Replace(ReplaceReferencesString, "");

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region MethodParams
        [Fact]
        public void ValidateAllowedMethodParams()
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

                                                public void Address1(Address param)
                                                {
                                                }
                                            }";

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateDisallowedMethodParams()
        {
            string adjustedSource = @"using System;
                                            using Stratis.SmartContracts;

                                            public class Test : SmartContract
                                            {
                                                public Test(ISmartContractState state)
                                                    : base(state) {}         

                                                public void Int64(long param)
                                                {
                                                }

                                                public void DateTime1(DateTime param)
                                                {
                                                }

                                                public void F(float param)
                                                {
                                                }
                                            }";

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        #endregion

        #region SimpleContract

        [Fact]
        public void ValidateSimpleContract()
        {
            var adjustedSource = @"
                using System;
                using Stratis.SmartContracts;

                public class Token : SmartContract
                {
                    public Token(ISmartContractState state): base(state) 
                    {
                        Balances = PersistentState.GetMapping<ulong>(""Balances"");
                    }

                    public Address Owner
                    {
                        get
                        {
                            return PersistentState.GetObject<Address>(""Owner"");
                        }
                        private set
                        {
                            PersistentState.SetObject(""Owner"", value);
                        }
                    }

                    public ISmartContractMapping<ulong> Balances { get; set; }

                    [SmartContractInit]
                    public void Init()
                    {
                        Owner = Message.Sender;
                    }

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

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);

            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);

            Assert.True(result.IsValid);
        }
        #endregion

        [Fact]
        public void ValidateCantGetAroundWithExtensionMethods()
        {
            string adjustedSource = TestString.Replace(ReplaceCodeString, "\"testString\".Test();")
                .Replace(ReplaceReferencesString, "");

            adjustedSource += @"public static class Extension
                {
                    public static void Test(this string str)
                    {
                        int test = Environment.TickCount;
                    }
                }";

            byte[] assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
            SmartContractDecompilation decomp = this.decompiler.GetModuleDefinition(assemblyBytes);
            SmartContractValidationResult result = this.validator.Validate(decomp);
            Assert.False(result.IsValid);
        }

        // Below commented out because HttpClient shouldn't even be part of the compilation framework.
        // Unsure if we provide a different error for things like that? When assembly is not allowed.

        //[Fact]
        //public void ValidateHttpClientFails()
        //{
        //    string adjustedSource = TestString.Replace(ReplaceCodeString, "var test = new HttpClient().GetAsync(\"http://google.com\");")
        //        .Replace(ReplaceReferencesString, "using System.Net.Http;");

        //    var assemblyBytes = GetFileDllHelper.GetAssemblyBytesFromSource(adjustedSource);
        //    var decomp = _decompiler.GetModuleDefinition(assemblyBytes);
        //    var result = _validator.Validate(decomp);
        //    Assert.False(result.Valid);
        //}
    }
}
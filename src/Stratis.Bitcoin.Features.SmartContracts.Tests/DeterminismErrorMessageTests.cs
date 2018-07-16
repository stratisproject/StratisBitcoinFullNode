using System.Linq;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public sealed class DeterminismErrorMessageTests
    {
        private readonly SmartContractDeterminismValidator validator = new SmartContractDeterminismValidator();

        [Fact]
        public void Validate_Determinism_ErrorMessages_Simple()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class MessageTest : SmartContract
                    {
                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestFloat()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("Float usage", result.Errors.First().ValidationType);
        }

        [Fact]
        public void Validate_Determinism_ErrorMessages_Simple_DateTime()
        {
            string source = @"
                    using Stratis.SmartContracts;
                    using System;

                    public class MessageTest : SmartContract
                    {
                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestDateTime()
                        {
                            var test = DateTime.Now;
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal(SmartContractDeterminismValidator.NonDeterministicMethodReference, result.Errors.First().ValidationType);
        }

        [Fact]
        public void Validate_Determinism_ErrorMessages_TwoMethods()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class MessageTest : SmartContract
                    {
                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestFloat1()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }

                        public void MessageTestFloat2()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Equal(2, result.Errors.Count());
            Assert.Equal("Float usage", result.Errors.First().ValidationType);
            Assert.Equal("Float usage", result.Errors.Skip(1).Take(1).First().ValidationType);
        }

        [Fact]
        public void Validate_Determinism_ErrorMessages_ThreeMethods_OneValid()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class MessageTest : SmartContract
                    {
                        private int test = 0;

                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestFloat1()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }

                        public void MessageTestFloat2()
                        {
                            this.test = 5;
                        }

                        public void MessageTestFloat3()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Equal(2, result.Errors.Count());
            Assert.Equal("Float usage", result.Errors.First().ValidationType);
            Assert.Equal("Float usage", result.Errors.Skip(1).Take(1).First().ValidationType);
        }

        [Fact]
        public void Validate_Determinism_ErrorMessages_Referenced_OneLevel()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class MessageTest : SmartContract
                    {
                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestValid()
                        {
                            MessageTestFloat1();
                        }

                        public void MessageTestFloat1()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("Float usage", result.Errors.First().ValidationType);
        }

        [Fact]
        public void Validate_Determinism_ErrorMessages_Referenced_TwoLevels()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class MessageTest : SmartContract
                    {
                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestValid()
                        {
                            MessageTestValid1();
                        }

                        public void MessageTestValid1()
                        {
                            MessageTestFloat1();
                        }

                        public void MessageTestFloat1()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("Float usage", result.Errors.First().ValidationType);
        }

        [Fact]
        public void Validate_Determinism_ErrorMessages_Referenced_ThreeLevels()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class MessageTest : SmartContract
                    {
                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestValid()
                        {
                            MessageTestValid1();
                        }

                        public void MessageTestValid1()
                        {
                            MessageTestValid2();
                        }

                        public void MessageTestValid2()
                        {
                            MessageTestFloat1();
                        }

                        public void MessageTestFloat1()
                        {
                            float test = (float) 3.5; test = test + 1;
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("Float usage", result.Errors.First().ValidationType);
        }

        [Fact]
        public void Validate_Determinism_ErrorMessages_Recursion_OneLevel()
        {
            string source = @"
                    using Stratis.SmartContracts;

                    public class MessageTest : SmartContract
                    {
                        public MessageTest(ISmartContractState smartContractState)
                            : base(smartContractState)
                        {
                        }

                        public void MessageTestValid()
                        {
                            MessageTestValid1();
                        }

                        public void MessageTestValid1()
                        {
                            float test = (float)3.5; 
                            test = test + 1;
                            MessageTestValid();
                        }
                    }";

            SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);
            Assert.True(compilationResult.Success);

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilationResult.Compilation);
            SmartContractValidationResult result = this.validator.Validate(decompilation);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("Float usage", result.Errors.First().ValidationType);
        }
    }
}
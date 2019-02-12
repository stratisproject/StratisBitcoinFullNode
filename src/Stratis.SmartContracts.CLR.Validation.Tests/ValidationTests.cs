using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Mono.Cecil;
using Stratis.SmartContracts.CLR.Validation.Policy;
using Stratis.SmartContracts.CLR.Validation.Validators;
using Stratis.SmartContracts.CLR.Validation.Validators.Type;
using Xunit;

namespace Stratis.SmartContracts.CLR.Validation.Tests
{
    public class ValidationTests
    {
        // TypePolicyValidator

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Allowed_Return_Type()
        {
            const string source = @"public class Test {public string A(){return ""a"";}}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type("Object", AccessPolicy.Allowed)
                      .Type("Void", AccessPolicy.Allowed)
                      .Type("String", AccessPolicy.Allowed));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Denied_Return_Type()
        {
            const string source = @"using System; public class Test {public DateTime A(){return new DateTime();}}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Using_Denied_MethodReference()
        {
            const string source = @"public class Test {public void A(){ var b = GetType();}}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Using_Denied_New_Type()
        {
            const string source = @"
using System; 
public class Test 
{
    public void A()
    { 
        var b = new DateTime();
    }
}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Using_Denied_Field()
        {
            const string source = @"
using System; 

public class Test 
{
    public void A()
    { 
        var b = BitConverter.IsLittleEndian;
    }
}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type(nameof(Boolean), AccessPolicy.Allowed)
                        .Type(nameof(BitConverter), AccessPolicy.Allowed, 
                            m => m.Member(nameof(BitConverter.IsLittleEndian), AccessPolicy.Denied))
                        .Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Using_Denied_Generic()
        {
            const string source = @"
using System; 
using System.Collections.Generic;

public class Test 
{
    public void A()
    { 
        var b = new List<string>();
    }
}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type(nameof(Boolean), AccessPolicy.Allowed)
                        .Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied))
                .Namespace("System.Collections.Generic", AccessPolicy.Allowed);

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Using_Denied_Nested_Generic()
        {
            const string source = @"
using System; 
using System.Collections.Generic;

public class Test 
{
    public void A()
    { 
        var b = new List<List<string>>();
    }
}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type(nameof(Boolean), AccessPolicy.Allowed)
                        .Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied))
                .Namespace("System.Collections.Generic", AccessPolicy.Allowed);

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Method_Using_Denied_Nested_Array_Element()
        {
            const string source = @"
using System; 
using System.Collections.Generic;

public class Test 
{
    public void A()
    { 
        var b = new [] { new [] { ""a"" } };
    }
}";

            var typeDefinition = CompileToTypeDef(source);            

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type(nameof(Boolean), AccessPolicy.Allowed)
                        .Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied))
                .Namespace("System.Collections.Generic", AccessPolicy.Allowed);

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();
            Assert.True(result.All(r => r is WhitelistValidator.DeniedTypeValidationResult));
        }

        [Fact]
        public void TypeDefValidator_Should_Validate_Nested_Type()
        {
            const string source = @"
using System; 

public class Test 
{
    public class C {
        public void A() {
            var b = new DateTime();
        }
    }
}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void TypeDefValidator_Should_Allow_References_To_Own_Methods()
        {
            const string source = @"
using System; 

public class Test 
{
    public void A() {
    }

    public void B() {
        A();
    }
}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TypePolicyValidator_Should_Validate_Own_Methods()
        {
            const string source = @"
using System; 

public class Test 
{
    static extern uint A();

    public void B() {
        var dt = DateTime.Now;
    }
}";

            var typeDefinition = CompileToTypeDef(source);

            var policy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t =>
                    t.Type("Object", AccessPolicy.Allowed)
                        .Type("Void", AccessPolicy.Allowed)
                        .Type("String", AccessPolicy.Denied));

            var validationPolicy = new ValidationPolicy()
                .WhitelistValidator(policy);

            var validator = new TypePolicyValidator(validationPolicy);

            var result = validator.Validate(typeDefinition).ToList();

            Assert.True(result.Any());
            Assert.True(result.All(r => r is WhitelistValidator.WhitelistValidationResult));
        }

        [Fact]
        public void NestedTypeIsValueTypeValidator_Should_Allow_Value_Type()
        {
            const string source = @"public class Test {public struct A{}}";

            var typeDefinition = CompileToTypeDef(source);

            var validator = new NestedTypeIsValueTypeValidator();

            var result = validator.Validate(typeDefinition.NestedTypes.First()).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void NestedTypeIsValueTypeValidator_Should_Not_Allow_Reference_Type()
        {
            const string source = @"public class Test {class A {}}";

            var typeDefinition = CompileToTypeDef(source);

            var validator = new NestedTypeIsValueTypeValidator();

            var result = validator.Validate(typeDefinition.NestedTypes.First()).ToList();

            Assert.Single(result);
            Assert.IsType<NestedTypeIsValueTypeValidator.NestedTypeIsValueTypeValidationResult>(result.Single());
        }

        [Fact]
        public void TypeHasMethodsValidator_Should_Validate_Type_Has_Methods()
        {
            const string source = @"public class Test{ int A(){ return 1; } void B(){} string C(){ return ""a"";} }";

            var typeDefinition = CompileToTypeDef(source);

            var validator = new TypeHasMethodsValidator();

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
            Assert.True(result.All(r => r is TypeHasMethodsValidator.TypeHasMethodsValidationResult));
        }

        [Fact] 
        public void TypeHasNestedTypesValidator_Should_Validate_Type_Has_Nested_Types_Class()
        {
            const string source = @"public class Test{ public class A {} }";

            var typeDefinition = CompileToTypeDef(source);

            var validator = new TypeHasNestedTypesValidator();

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
            Assert.True(result.All(r => r is TypeHasNestedTypesValidator.TypeHasNestedTypesValidationResult));
        }

        [Fact]
        public void TypeHasNestedTypesValidator_Should_Validate_Type_Has_Nested_Types_Struct()
        {
            const string source = @"public class Test{ public struct A {} }";

            var typeDefinition = CompileToTypeDef(source);

            var validator = new TypeHasNestedTypesValidator();

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Single(result);
            Assert.True(result.All(r => r is TypeHasNestedTypesValidator.TypeHasNestedTypesValidationResult));
        }

        [Fact]
        public void FieldDefinitionValidator_Should_Validate_Type_Has_Fields()
        {
            const string source = @"public class Test{ public string A; public int B; private int C; uint D; }";

            var typeDefinition = CompileToTypeDef(source);

            var validator = new FieldDefinitionValidator();

            var result = validator.Validate(typeDefinition).ToList();

            Assert.Equal(4, result.Count);
            Assert.True(result.All(r => r is FieldDefinitionValidator.FieldDefinitionValidationResult));
        }

        

        [Fact]
        public void StaticConstructorValidator_Should_Validate_Static_Constructor()
        {
            const string source = @"public class Test { static Test(){} }";

            var typeDefinition = CompileToTypeDef(source);

            var result = new StaticConstructorValidator().Validate(typeDefinition).ToList();

            Assert.Single(result);
            Assert.IsType<StaticConstructorValidator.StaticConstructorValidationResult>(result.Single());
        }

        [Fact]
        public void MethodParamValidator_Should_Validate_Allowed_Params()
        {
            const string source = @"using Stratis.SmartContracts; public class Test { 
                                                public void Bool(bool param){}
                                                public void Byte(byte param){}
                                                public void ByteArray(byte[] param){}
                                                public void Char(char param){}
                                                public void String(string param){}
                                                public void Int32(int param){}
                                                public void UInt32(uint param){}
                                                public void UInt64(ulong param){}
                                                public void Int64(long param){}
                                                public void Address1(Address param){}
            }";

            var typeDefinition = CompileToTypeDef(source);

            foreach (var methodDefinition in typeDefinition.Methods)
            {
                var result = new MethodParamValidator().Validate(methodDefinition).ToList();
                Assert.Empty(result);
            }
        }

        [Fact]
        public void MethodParamValidator_Should_Validate_Disallowed_Params()
        {
            const string source = @"using System; public class Test { 
                                                public void DateTime1(DateTime param){}
                                                public void F(float param){}
            }";

            var typeDefinition = CompileToTypeDef(source);

            foreach (var methodDefinition in typeDefinition.Methods)
            {
                var result = new MethodParamValidator().Validate(methodDefinition).ToList();
                Assert.True(result.All(r => r is MethodParamValidator.MethodParamValidationResult));
            }
        }

        [Fact]
        public void MethodParamValidator_Should_Validate_Optional_Params()
        {
            const string source = @"
using System;
public class Test { 
    public void OptionalTest(int optional = 1, int optional2 = 2){}
}";

            var typeDefinition = CompileToTypeDef(source);

            var method = typeDefinition.Methods.First(m => m.Name == "OptionalTest");

            var result = new MethodParamValidator().Validate(method).ToList();
            Assert.Equal(2, result.Count);
            Assert.True(result.All(r => r is MethodParamValidator.MethodParamValidationResult));
        }

        [Fact]
        public void FinalizerValidator_Should_Validate_OverrideFinalizer()
        {
            const string source = @"public class Test { ~Test(){} }";

            var typeDefinition = CompileToTypeDef(source);

            var result = new FinalizerValidator().Validate(typeDefinition).ToList();

            Assert.Single(result);
            Assert.IsType<FinalizerValidator.FinalizerValidationResult>(result.Single());
        }

        [Fact]
        public void FinalizerValidator_Should_Validate_MethodFinalizer()
        {
            const string source = @"public class Test { void Finalize(){} }";

            var typeDefinition = CompileToTypeDef(source);

            var result = new FinalizerValidator().Validate(typeDefinition).ToList();

            Assert.Single(result);
            Assert.IsType<FinalizerValidator.FinalizerValidationResult>(result.Single());
        }

        public TypeDefinition CompileToTypeDef(string source)
        {
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var smartContracts = MetadataReference.CreateFromFile(typeof(Address).Assembly.Location);
            var runtime = MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location);

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            CSharpCompilation compilation = CSharpCompilation.Create(
                "Test",
                new[] { syntaxTree },
                references: new[] { mscorlib, smartContracts, runtime },
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    checkOverflow: true));

            using (var dllStream = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(dllStream);
                if (!emitResult.Success)
                    throw new Exception("Compilation Failed");

                var assemblyBytes = dllStream.ToArray();

                var moduleDef = ModuleDefinition.ReadModule(new MemoryStream(assemblyBytes));

                return moduleDef.Types.First(t => !t.Name.Equals("<Module>"));
            }
        }
    }

    public class TestValidator : IMethodDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            return new[]
            {
                new MethodDefinitionValidationResult(method.Name, "", "")
            };
        }

    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using Stratis.SmartContracts.CLR.Validation.Validators.Module;
using Xunit;

namespace Stratis.SmartContracts.CLR.Validation.Tests
{
    public class AssemblyReferenceValidatorTests
    {
        private const string AssemblyName = "Test";
        private const string ModuleName = "A Module";
        private const string Version1 = "1.0.0.0";
        private const string Version2 = "1.0.1.0";

        [Fact]
        public void VersionIsntTakenIntoAccount()
        {
            var allowedAssembly = new TestAssembly(Version1);

            var assemblyReferenceValidator = new AssemblyReferenceValidator(new Assembly[] {allowedAssembly});

            ModuleDefinition testModule = ModuleDefinition.CreateModule(ModuleName, ModuleKind.Dll);
            testModule.AssemblyReferences.Add(new AssemblyNameReference(AssemblyName, Version.Parse(Version2)));

            IEnumerable<ValidationResult> result = assemblyReferenceValidator.Validate(testModule);

            Assert.Empty(result);
        }

        public class TestAssembly : Assembly
        {
            private readonly string version;

            public TestAssembly(string version)
            {
                this.version = version;
            }

            public override AssemblyName GetName()
            {
                var assemblyName = new AssemblyName(AssemblyName);
                assemblyName.Version = Version.Parse(this.version);
                return assemblyName;
            }
        }
    }
}

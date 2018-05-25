using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using Mono.Cecil;
using Stratis.SmartContracts.Core.ContractValidation;

namespace Stratis.System.Inspector
{
    class Program
    {
        private static IEnumerable<IMethodDefinitionValidator> _validators = new List<IMethodDefinitionValidator>
        {
            new NativeMethodFlagValidator(),
            new PInvokeImplFlagValidator(),
            new UnmanagedFlagValidator(),
            new InternalFlagValidator(),
            new MethodAllowedTypeValidator(),
            new GetHashCodeValidator(),
            new MethodInstructionValidator()
        };

        static void Main(string[] args)
        {
            var objName = typeof(object).Module.FullyQualifiedName;

            var obj = AssemblyDefinition.ReadAssembly(objName);

            Console.WriteLine($"Inspecting {objName}");

            var allTypes = obj.Modules.SelectMany(o => o.Types).ToList();
            var allMethods = allTypes.SelectMany(t => t.Methods).ToList();

            Console.WriteLine($"Found {obj.Modules.Count} modules");
            Console.WriteLine($"Found {allTypes.Count} types");
            Console.WriteLine($"Found {allMethods.Count} methods");

            foreach (var module in obj.Modules)
            {
                var allResults = new List<object>();

                Console.WriteLine($"Inspecting {module.Name}");

                Console.WriteLine($"Module {module.Name} contains {module.Types.Count} types");

                foreach (var type in module.Types)
                {
                    Console.WriteLine($"Inspecting type {type.FullName}");
                    Console.WriteLine($"Type {type.FullName} contains {type.Methods.Count} methods");

                    foreach (var method in type.Methods)
                    {
                        Console.WriteLine($"Inspecting method {method.FullName}");
                        var inspectionResults = Inspect(method);
                        var returnType = method.ReturnType;

                        allResults.AddRange(inspectionResults.Select(r =>
                        new
                        {
                            r.MethodName,
                            r.MethodFullName,
                            ReturnType = returnType.FullName,
                            r.ErrorType,
                            r.Message
                        }));
                    }
                }

                using (var csv = new CsvWriter(new StreamWriter($@"{module.Name}-Analysis.csv")))
                {
                    csv.WriteRecords(allResults);
                }

            }

            Console.ReadKey();
        }

        private static IEnumerable<FormatValidationError> Inspect(MethodDefinition method)
        {
            var validationResults = new List<FormatValidationError>();

            foreach (var validator in _validators)
            {
                var validationResult = validator.Validate(method);

                validationResults.AddRange(validationResult);
            }

            return validationResults;
        }
    }
}

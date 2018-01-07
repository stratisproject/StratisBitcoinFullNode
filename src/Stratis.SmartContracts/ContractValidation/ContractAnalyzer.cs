//using System;
//using System.Collections.Generic;
//using System.Text;
//using Mono.Cecil;
//using System.IO;
//using Stratis.SmartContracts.ContractValidation.Result;

//namespace Stratis.SmartContracts.ContractValidation
//{
//    internal class ContractAnalyzer
//    {
//        private ModuleDefinition _moduleDefinition;
//        private TypeDefinition _contractType;
//        private TypeDefinition _baseType;
//        private IList<ISmartContractValidator> _validators;
//        private SpendGasInjector _spendGasInjector;

//        public ContractAnalyzer(byte[] contractCode, string contractName)
//        {
//            // TODO: Ensure that AppContext.BaseDirectory is robust here
//            var resolver = new DefaultAssemblyResolver();
//            resolver.AddSearchDirectory(AppContext.BaseDirectory);
//            _moduleDefinition = ModuleDefinition.ReadModule(new MemoryStream(contractCode), new ReaderParameters { AssemblyResolver = resolver });
//            _contractType = _moduleDefinition.GetType(contractName);
//            _baseType = _contractType.BaseType.Resolve();

//            _validators = new List<ISmartContractValidator>
//            {
//                new SmartContractFormatValidator(_moduleDefinition, _contractType),
//                new SmartContractDeterminismValidator(_contractType)
//            };

//            _spendGasInjector = new SpendGasInjector();
//        }


//        /// <summary>
//        /// At the moment this will just throw an exception if the contract isn't crafted properly. In the future we should look to make this
//        /// more robust - output all of the errors and return a bool.
//        /// </summary>
//        public SmartContractValidationResult ValidateContract()
//        {
//            var endResult = new SmartContractValidationResult();
//            foreach(var validator in _validators)
//            {
//                endResult.Errors.AddRange(validator.Validate().Errors);
//            }
//            return endResult;
//        }

//        public byte[] InjectSpendGas()
//        {
//            _spendGasInjector.AddGasCalculationToContract(_contractType, _baseType);
//            var mem = new MemoryStream();
//            _moduleDefinition.Write(mem);
//            return mem.ToArray();
//        }
//    }
//}

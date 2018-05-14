using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.Lifecycle;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        private readonly InternalTransactionExecutorFactory internalTransactionExecutorFactory;
        private readonly ILogger logger;
        private readonly IPersistentState persistentState;
        private readonly IContractStateRepository repository;
        public static int VmVersion = 1;

        public ReflectionVirtualMachine(InternalTransactionExecutorFactory internalTransactionExecutorFactory, ILoggerFactory loggerFactory, IPersistentState persistentState, IContractStateRepository repository)
        {
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.persistentState = persistentState;
            this.repository = repository;
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public ISmartContractExecutionResult Create(byte[] contractCode, ISmartContractExecutionContext context, IGasMeter gasMeter)
        {
            this.logger.LogTrace("()");

            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToConstructor(contractCode);

            Type contractType = Load(gasInjectedCode);

            var internalTransferList = new InternalTransferList();

            IInternalTransactionExecutor internalTransactionExecutor = this.internalTransactionExecutorFactory.Create(this.repository, internalTransferList);

            var balanceState = new BalanceState(this.repository, context.Message.Value, internalTransferList);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                this.persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(context.ContractAddress));

            // Invoke the constructor of the provided contract code
            LifecycleResult result = SmartContractConstructor
                .Construct(contractType, contractState, context.Parameters);

            ISmartContractExecutionResult executionResult = new SmartContractExecutionResult
            {
                GasConsumed = gasMeter.GasConsumed
            };

            if (!result.Success)
            {
                this.logger.LogTrace("(-)[CREATE_CONTRACT_INSTANTIATION_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);
                executionResult.Exception = result.Exception.InnerException ?? result.Exception;
                return executionResult;
            }

            executionResult.Return = result.Object;

            this.logger.LogTrace("(-):{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

            return executionResult;
        }

        /// <summary>
        /// Invokes a method on an existing smart contract
        /// </summary>
        public ISmartContractExecutionResult ExecuteMethod(byte[] contractCode,
            string contractMethodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter)
        {
            ISmartContractExecutionResult executionResult = new SmartContractExecutionResult();

            if (contractMethodName == null)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_METHODNAME_NOT_GIVEN]");
                return executionResult;
            }

            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToContractMethod(contractCode, contractMethodName);
            Type contractType = Load(gasInjectedCode);
            if (contractType == null)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_CONTRACTTYPE_NULL]");
                return executionResult;
            }

            var internalTransferList = new InternalTransferList();

            IInternalTransactionExecutor internalTransactionExecutor =
                this.internalTransactionExecutorFactory.Create(this.repository, internalTransferList);

            var balanceState = new BalanceState(this.repository, context.Message.Value, internalTransferList);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                this.persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(context.ContractAddress));

            LifecycleResult result = SmartContractRestorer.Restore(contractType, contractState);

            if (!result.Success)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_INSTANTIATION_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);
                executionResult.Exception = result.Exception.InnerException ?? result.Exception;
                executionResult.GasConsumed = gasMeter.GasConsumed;
                return executionResult;
            }

            try
            {
                MethodInfo methodToInvoke = contractType.GetMethod(contractMethodName);
                if (methodToInvoke == null)
                {
                    var errorMessage = string.Format("[CALLCONTRACT_METHODTOINVOKE_NULL_DOESNOT_EXIST]:{0}={1}", nameof(contractMethodName), contractMethodName);
                    this.logger.LogTrace("(-){0}", errorMessage);
                    throw new ArgumentException(errorMessage);
                }

                if (methodToInvoke.IsConstructor)
                {
                    this.logger.LogTrace("(-)[CALLCONTRACT_CANNOT_INVOKE_CTOR]");
                    throw new ConstructorInvocationException("Cannot invoke constructor");
                }

                SmartContract smartContract = result.Object;
                executionResult.Return = methodToInvoke.Invoke(smartContract, context.Parameters);

                executionResult.InternalTransfers = internalTransferList.Transfers;
            }
            catch (ArgumentException argumentException)
            {
                this.logger.LogTrace("{0}", argumentException.Message);
                executionResult.Exception = argumentException;
            }
            catch (TargetInvocationException targetException)
            {
                this.logger.LogTrace("{0}", targetException.Message);
                executionResult.Exception = targetException.InnerException ?? targetException;
            }
            catch (TargetParameterCountException parameterExcepion)
            {
                this.logger.LogTrace("{0}", parameterExcepion.Message);
                executionResult.Exception = parameterExcepion;
            }
            catch (ConstructorInvocationException constructorInvocationException)
            {
                this.logger.LogTrace("{0}", constructorInvocationException.Message);
                executionResult.Exception = constructorInvocationException;
            }
            finally
            {
                executionResult.GasConsumed = gasMeter.GasConsumed;
            }

            this.logger.LogTrace("(-):{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

            return executionResult;
        }

        /// <summary>
        /// Loads the Assembly bytecode into the current AppDomain
        /// </summary>
        /// <param name="byteCode"></param>
        /// <returns></returns>
        private static Type Load(byte[] byteCode)
        {
            Assembly contractAssembly = Assembly.Load(byteCode);

            // The contract should always be the only exported type
            return contractAssembly.ExportedTypes.FirstOrDefault();
        }
    }
}
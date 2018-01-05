using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Hashing;
using NBitcoin;

namespace Stratis.SmartContracts.Backend
{
    //internal class StandardTransactionExecutor : ITransactionExecutor
    //{
    //    private ISmartContractStateRepository _stateDb;

    //    public StandardTransactionExecutor(ISmartContractStateRepository stateDb)
    //    {
    //        _stateDb = stateDb;
    //    }

    //    public ExecutionResult Execute(TestTransaction transaction)
    //    {
    //        // increase transaction counter for account
    //        //_stateDb.IncrementNonce(transaction.From);

    //        if (transaction.IsContractCreation)
    //        {
    //            return Create(transaction);
    //        }
    //        else
    //        {
    //            return Call(transaction);
    //        }
    //    }
        
    //    // TODO: First update so all of the uint stuff is correct, then look into updating the utxoSet

    //    private ExecutionResult Create(TestTransaction transaction)
    //    {
    //        //var newContractHash = HashHelper.NewContractAddress(transaction.From.ToBytes(), new uint256(_stateDb.GetNonce(transaction.From)).ToBytes());
    //        var newContractHash = HashHelper.NewContractAddress(transaction.From.ToBytes(), new uint256(0).ToBytes());
    //        uint160 newContractAddress = new uint160(newContractHash); 

    //        _stateDb.CreateAccount(newContractAddress);

    //        // Mock execution context based on transaction and block context
    //        // Of course more data goes here, this is just a demo
    //        var executionContext = new ExecutionContext
    //        {
    //            BlockNumber = transaction.BlockNumber,
    //            BlockHash = 256,
    //            CoinbaseAddress = 4,
    //            Difficulty = 1,
    //            CallValue = transaction.Value,
    //            CallerAddress = transaction.From,
    //            ContractAddress = newContractAddress,
    //            ContractTypeName = transaction.ContractTypeName,
    //            GasLimit = transaction.GasLimit,
    //            Parameters = transaction.Parameters
    //        };

    //        //_stateDb.AddBalance(newContractAddress, transaction.Value);
    //        //_stateDb.SubtractBalance(transaction.From, transaction.Value);

    //        // Create virtual machine with our k/v store
    //        ISmartContractVirtualMachine virtualMachine = new ReflectionVirtualMachine(_stateDb);
    //        // Load contract and run - magic happens in here
    //        var result =  virtualMachine.CreateContract(transaction.Data, executionContext);

    //        _stateDb.SetCode(newContractAddress, (byte[]) result.Return); // set contract code
    //        return result;
    //    }

    //    private ExecutionResult Call(TestTransaction transaction)
    //    {
    //        var contractCode = _stateDb.GetCode(transaction.To);

    //        var executionContext = new ExecutionContext
    //        {
    //            BlockNumber = transaction.BlockNumber,
    //            BlockHash = 256,
    //            CoinbaseAddress = 4,
    //            Difficulty = 1,
    //            CallValue = transaction.Value,
    //            CallerAddress = transaction.From,
    //            ContractAddress = transaction.To,
    //            ContractTypeName = transaction.ContractTypeName,
    //            ContractMethod = transaction.ContractMethodName,
    //            GasLimit = transaction.GasLimit,
    //            Parameters = transaction.Parameters
    //        };

    //        //_stateDb.AddBalance(transaction.To, transaction.Value);
    //        //_stateDb.SubtractBalance(transaction.From, transaction.Value);

    //        // Create virtual machine with our k/v store
    //        ISmartContractVirtualMachine virtualMachine = new ReflectionVirtualMachine(_stateDb);
    //        // Load contract and run - magic happens in here
    //        return virtualMachine.LoadContractAndRun(contractCode, executionContext);
    //    }
    //}
}

# Smart Contracts Architecture

## Abstract

Smart Contracts allow executable code to be run on top of a blockchain. Contracts enable new ways to coordinate the transfer of value (coins) that are difficult or impossible with UTXOs alone.

Stratis' Smart Contracts platform has been architected with these considerations:
- Integrate contract execution environments on top of an existing UTXO-based blockchain via an abstraction layer
- Enable modular contract execution environments
- Provide an implementation of a CIL execution environment

This design allows for a fully modular smart contract platform, where developers can create new contract execution environments without needing to modify the underlying chain.

## High level design overview

The core of Stratis smart contracts is implemented in a single project - **Stratis.SmartContracts.Core**. This contains the abstraction layer neccessary to perform contract execution on top of the underlying UTXO-based chain.

Other projects are all related to the CIL execution environment:
* **Stratis.SmartContracts** - Contains components for writing contracts in C#/CIL.
* **Stratis.SmartContracts.Executor.Reflection** - Contains the implementation of the CIL contract execution environment.
* **Stratis.SmartContracts.Core.Validation** - Contains components used for validating CIL contracts for deterministic execution and format.
* **Stratis.Bitcoin.Features.SmartContracts** - Contains the feature definition for integrating the CIL executor with the Stratis full node.
* **Stratis.Bitcoin.Features.SmartContracts.Wallet** - Contains the smart contract wallet feature definition.
* **Stratis.SmartContracts.Tools.Sct** - SCT (Smart Contract Tool) is a command-line tool used to validate and compile C# contracts.

## Implementing an Execution Environment

A new smart contract execution environment must implement `Stratis.SmartContracts.Core.IContractExecutor`. `IContractExecutor` has this signature:

``` csharp
    public interface IContractExecutor
    {
        IContractExecutionResult Execute(IContractTransactionContext transactionContext);
    }
```

An execution environment can be though of as a 'black box' that defines a transition between two states of the blockchain. When a contract execution takes place, these things will always happen:
* Some data is received from a transaction on chain (`IContractTransactionContext`)
* That data is acted upon (`IContractExecutor.Execute`)
* A result is returned which changes the state of the chain (`IContractExecutionResult`)

### Contract Transaction Context

When a contract execution transaction is received, an `IContractTransactionContext` object is provided to the execution environment. This object contains all the data of the smart contract transaction as well as information about the current state of the chain.

It should be possible to complete a contract execution without relying on any other components for information about the chain.

### Contract Execution Result

After a contract execution has occurred, an `IContractExecutionResult` object is returned to the contract abstraction layer. This object contains information about the outcome of contract processing, such as the quantity of Gas consumed, any logs generated, and any internal transactions that occurred.

### Contract Executor

The executor defines the state transition that occurs based on the transaction input received. It is responsible for updating the contract state database, and returning the results of execution to the abstraction layer.

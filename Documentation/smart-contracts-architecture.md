# Smart Contracts Architecture

## Abstract

Smart Contracts allow executable code to be run on top of a blockchain. Contracts enable new ways to coordinate the transfer of value (coins) that are difficult or impossible with UTXOs alone.

Stratis' Smart Contracts platform has been architected with these considerations:
- Integrate contract execution environments on top of an existing UTXO-based blockchain via an abstraction layer
- Enable modular contract execution environments
- Provide an implementation of a CIL execution environment

This design allows for a fully modular smart contract platform, where developers can create new contract execution environments without needing to modify the underlying chain.

## High-Level Design Overview

The core of Stratis smart contracts is implemented in a single project - **Stratis.SmartContracts.Core**. This contains the abstraction layer neccessary to perform contract execution on top of the underlying UTXO-based chain.

Other projects are all related to the CIL execution environment:
* **Stratis.SmartContracts** - Contains components for writing contracts in C#/CIL.
* **Stratis.SmartContracts.Executor.Reflection** - Contains the implementation of the CIL contract execution environment.
* **Stratis.SmartContracts.Core.Validation** - Contains components used for validating CIL contracts for deterministic execution and format.
* **Stratis.Bitcoin.Features.SmartContracts** - Contains the feature definition for integrating the CIL executor with the Stratis full node.
* **Stratis.Bitcoin.Features.SmartContracts.Wallet** - Contains the smart contract wallet feature definition.
* **Stratis.SmartContracts.Tools.Sct** - SCT (Smart Contract Tool) is a command-line tool used to validate and compile C# contracts.

## Modular Execution Environments

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

In the current architecture, the Contract Executor must have these properties:
* **Deterministic execution.** Contracts must execute deterministically. A contract execution must produce the same output every time it is run with the same input, regardless of the time, location, platform or architecture it is being run on.
* **Bounded execution.** Contracts must halt. They must not run forever. A contract's usage of execution resources must have an upper bound.

### Integration With The Full Node

Integrating a contract executor with the full node can be done by defining a new `FullNodeFeature`. This feature defines the dependencies that will be wired up by the DI framework upon starting the full node.

For a sample implementation, see the **Stratis.Bitcoin.Features.SmartContracts** project.

## The CIL Executor

The CIL executor is an execution environment for running [Common Intermediate Language](https://en.wikipedia.org/wiki/Common_Intermediate_Language) bytecode contracts.

Any CIL bytecode that passes validation can be executed, however the CIL executor has been specifically designed with Roslyn-compiled C# CIL in mind.

### Overview

An overview of what occurs in the CIL executor implementation (Stratis.SmartContracts.Executor.Reflection) is as follows: 

* An `IContractTransactionContext` object is received
* Contract invocation data is deserialized from the raw bytes in the `IContractTransactionContext.Data` field
* The `ReflectionVirtualMachine` is invoked with a contract create/call, the transaction data, and the current state of the chain
* The `ReflectionVirtualMachine` executes the bytecode of the contract
* If execution was successful, the account state and contract state database is updated
* If a contract was created, its address is returned as `IContractExecutionResult.NewContractAddress`
* Internally generated transactions are condensed into a single transaction `IContractExecutionResult.InternalTransaction`
* The gas refund is returned as a TxOut on `IContractExecutionResult.Refund`
* Logs are returned as `IContractExecutionResult.Logs`
* The execution result is returned to the abstraction layer
* The abstraction layer updates the UTXO set with the value transfers that took place during execution
# Smart Contracts Architecture

## Abstract

Smart Contracts allow executable code to be run on top of a blockchain. Contracts enable new ways to coordinate the transfer of value (coins) that are difficult or impossible with UTXOs alone.

Stratis' Smart Contracts platform has been architected with these considerations:
- Integrate contract execution with an existing UTXO-based blockchain
- Enable modular contract execution environments
- Provide an implementation of a CIL execution environment

This design allows for a fully modular smart contract platform, where developers can create new contract execution environments without needing to modify the underlying chain.

## High level design overview

The core of Stratis smart contracts is implemented in a single project - **Stratis.SmartContracts.Core**. This contains the interfaces that a contract execution environment must implement. It abstracts contract execution on top of the underlying UTXO-based chain.

Other projects are all related to the CIL execution environment:
* **Stratis.SmartContracts** - Contains components for writing contracts in C#/CIL.
* **Stratis.SmartContracts.Executor.Reflection** - Contains the implementation of the CIL contract execution environment.
* **Stratis.SmartContracts.Core.Validation** - Contains components used for validating CIL contracts for deterministic execution and format.
* **Stratis.Bitcoin.Features.SmartContracts** - Contains the feature definition for integrating the CIL executor with the Stratis full node.
* **Stratis.Bitcoin.Features.SmartContracts.Wallet** - Contains the smart contract wallet feature definition.
* **Stratis.SmartContracts.Tools.Sct** - SCT (Smart Contract Tool) is a command-line tool used to validate and compile C# contracts.
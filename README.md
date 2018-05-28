# Stratis Smart Contracts in C# - Alpha Relase
Stratis has engineered the first smart contracts platform built entirely from the ground up in native C# on the .NET framework, the most popular enterprise programming language on the most widely-used enterprise framework.
# Documentation
Documentation can be found in the below URL.
https://smartcontractsdocs.stratisplatform.com
You can also obtain TSTRAT for the Smart Contracts Testnet from our faucet in the below URL.
https://smartcontractsfaucet.stratisplatform.com/
# Support
For any support and/or development enquires regarding the Stratis Smart Contracts solution, please join our Discord Smart Contracts channel.
https://Discord

# Release Notes

v0.9.0
- Long (UInt64) is now a supported type.
- Receipts are now generated when smart contracts are executed. The Sct tool also returns the transaction hash that the contract should be deployed inside. With all of this developers can now receive feedback about the outcome of contract creations or calls via the API.
- Fixed a bug where transactions weren't reaching the mempool when deployed via the Sct tool. This was due to the wallet trying to build transactions using transaction outputs that had previously failed to be included in a block.
- The methods for storage of types are now more explicit, to clearly demonstrate to developers what is and is not serializable.
- Gas prices have been adjusted so that storage operations are now relatively more expensive than other operations.
- Try/catch blocks are no longer allowed inside smart contracts as certain properties of thrown exceptions are non-deterministic.
- General usability fixes to the command-line tool.

v0.8.2-alpha
- Significant improvements to the underlying architecture to improve stability
- Sct tool now displays warnings when fields are declared in a smart contract, to avoid confusion about what data is persisted between calls

v0.8.1-alpha
- Removed get spendable balance check from SmartContractController.
- Added extra logging to SmartContractController and ReflectionVirtualMachine.

v0.8.0-alpha
- Fixed an OutOfIndexException bug that was causing contracts to randomly fail deployment.
- Updated Sct tool to check that the required constructor parameters are input before attempting to deploy.
- Updated UserAgent and Version string to identify Smart Contract nodes.
- Various wording amendments to Sct and API output to improve usability.

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

v0.8.1-alpha
- Removed get spendable balance check from SmartContractController.
- Added extra logging to SmartContractController and ReflectionVirtualMachine.

v0.8.0-alpha
- Fixed an OutOfIndexException bug that was causing contracts to randomly fail deployment.
- Updated Sct tool to check that the required constructor parameters are input before attempting to deploy.
- Updated UserAgent and Version string to identify Smart Contract nodes.
- Various wording amendments to Sct and API output to improve usability.
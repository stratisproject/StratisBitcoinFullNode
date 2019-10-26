# Smart Contract Whitelisting

The PoA voting feature provides the federation members the ability to vote on a list of whitelisted hashes. The smart contracts project integrates with this feature by adding a new consensus and mempool rule. The rule hashes the bytecode contained in any contract creation transactions. This hash is checked against the whitelist. If the hash is not whitelisted, contract deployment fails.

Practically, this means that it will only be possible to deploy contracts whose code hash is present on the federation whitelist.

## Usage

Enable contract whitelisting by providing an option to the smart contracts feature in the node builder.

```
    .AddSmartContracts(options =>
    {
        options.UsePoAWhitelistedContracts();
    })
```

Whitelisted contracts should only be enabled on a node using `.UseSmartContractPoAConsensus()`. The node will fail to run correctly if this is not true.

## Important architectural details

### Hashing algorithm
Keccak256 is used to hash contract bytecode. This can be changed by defining a different implementation of `IContractCodeHashingStrategy`. Hashes must be 32 bytes wide.

### Use of full validation rule
Enabling contract whitelisting adds a new full-validation consensus rule `AllowedCodeHashLogic`. This rule must be full-validation due to the whitelisted hashes repository also being updated in a FV rule. Consider the following scenario if it were a partial validation rule: a node permitted a contract deployment in block 10. The consensus tip is at 2 and at 5 the hash is removed. After block 5 + max reorg (1 for this example) it will no longer be a valid contract deployment tx due to the code hash being removed from the whitelist.

### Mempool validation
The mempool will pre-validate incoming transactions using the same consensus rule. If validation fails, the transactions will not be considered for inclusion into a block. It is still possible that a contract transaction accepted into the mempool is not included into a block. The contract's code hash may be removed from the whitelist between the time the transaction is added to the mempool and the time it is mined.

### Use of dependency injection for the rule
The DI container will automatically inject all registered `IContractTransactionFullValidationRule` implementations into the `SmartContractPoARuleRegistration`. These are passed into `ContractTransactionFullValidationRule` which applies rules to validate the contract transaction format.
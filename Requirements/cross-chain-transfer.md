# Suggestion for Cross Chain transfers mechanism

## 1) All federation members nodes online

### A cross chain transfer is initiated

Some users of the chain A decide to transfer funds to the chain B. To achieve that, they eacyh create a deposit to the multisig address of the federation on chain A. Now they will need to wait for as many blocks as the maximum reorg length before the transfer mechanism of the federation triggers.

![A cross chain transfer is initiated](../assets/cross-chain-transfers/happy-path-1.svg)

### The cross chain transfer is triggered after maximum reorg length

As the maximum reorg length is passed, and the deposit transactions cannot be reverted, the federation starts to proceed with the transfer. A prefered leader for that transfer is elected deterministically based on the current heighty of chain A, but each node still performs the same operations independently. Each node transfers the details of the multisig deposits made on chain A (block height, target addresses and corresponding amounts to transfer), then persists those details in their database.
From that point, each nodes has enough information to create partially signed transactions and propagate them on the network. From that point, each node will also persist the partially signed transactions but only the leader will be able to broadcast a fully signed one.

![The cross chain transfer is triggered after MaxReorg](../assets/cross-chain-transfers/happy-path-2.svg)

### The cross chain transfer appears on targeted chain

Once the leader has collected enough signatures, he can broadcast a fully signed transaction to the network and make it appear in the mempool of all the other nodes. This transaction will then be persisted in the next block by another node in the chain B network.
Once this is done, each federation member monitoring the chain B will match the transaction found on chain by its inputs, outputs (including the OP_RETURN), and consider the transfer done.
_We might need to wait MaxReorg maturity to consider the transfer fully done_

![The cross chain transfer appears on targeted chain](../assets/cross-chain-transfers/happy-path-3.svg)

## 2) Preferred leader offline

### A cross chain transfer is triggered while leader is offline

Similar to the previous case, deposit transactions to the federation's multisig on chain A have triggered a cross chain transfer. Here however, the prefered leader happens to be offline. All other nodes carry on as usual, creating and exchanging partially signed transactions. However, no one is here to broadcast it as FM<sub>2</sub> is offline.

![A cross chain transfer is triggered while leader is offline](../assets/cross-chain-transfers/leader-offline-1.svg)

### The cross chain is handled one block later by next leader

As time passes, a new block appears on chain A, and a new leader is elected (FM<sub>3</sub>). This new leader can now broadcast the transaction and we are back on the normal path (cf 1.).

![The cross chain is handled one block later by next leader](../assets/cross-chain-transfers/leader-offline-2.svg)

### The leader comes back online

When the leader comes back online, it will retrieve from its stores what was the last known block heights for which it had dealt with transfers, and resync its store from there while synching up its nodes. Partial transactions will then be added and removed from the store depending on there status on the chain B.
The node will only be able to participate in cross chain transfers when it is fully synced again, and has access to the latest version of the utxos on each multisigs.

![The leader comes back online](../assets/cross-chain-transfers/leader-offline-3.svg)

## 3) Building the chain B transaction deterministically

The main point of building transactions deterministically is to allow for each member to independently build the exact same transaction. This way the can uniquely identify transactions coming from other members and decide to match them with their own (to collect signatures, change their statuses when finding them on chain, or when recovering from a period offline).

![Building transactions](../assets/cross-chain-transfers/building-transaction.svg)

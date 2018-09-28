# Suggestion for Cross Chain transfers mechanism

### Overview

Coordinating transfer between two chains requires a federation of members to work together in order to achieve enough signatures to release funds from the federation's multisig.  
We propose a deterministic approach to generating such transactions and to select the leader that will coordinate the process of transfers.  
We assume that the federation members are able to connect to each other and broadcast messages between themselves.

## 1) All federation members nodes online

### A cross chain transfer is initiated

Some users of chain A decide to transfer funds to chain B. To achieve that, they each create a deposit to the multisig address of the federation on chain A. Now they will need to wait for the transfer trx to be buried under enough blocks, the maximum reorg length if activated, before the transfer mechanism of the federation is triggered.

![A cross chain transfer is initiated](../assets/cross-chain-transfers/happy-path-1.svg)

### The cross chain transfer is triggered after chain A's maximum reorg length

When the maximum reorg length is passed and the deposit transactions cannot be reverted the federation members start to proceed with the transfer. A leader for that transfer is elected deterministically based on the block height in which the transfer transaction was confirmed on chain A, but each node still performs the same operations independently.  

Before the leader can handle the transactions in its current block she must first look into previous blocks and detect if there was any unprocessed transfer, it's important to note that a transfer cannot happen until all previous transfers have been processed successfully, this means a leader is also responsible for blocks belonging to other members in case they were offline.  

Each node transfers the details of the multisig deposits made on chain A (block height, target addresses and corresponding amounts to transfer), then persists those details in its database.  
From that point, each node has enough information to create partially signed transactions and propagate them to the other members of the federation. Each node will also persist the partially signed transactions they receive and collect signatures (this is in case they become the leader of an unprocessed transfer and can immediately broadcast it), but only the leader will proceed to broadcasting a fully signed one.

![The cross chain transfer is triggered after MaxReorg](../assets/cross-chain-transfers/happy-path-2.svg)

### The cross chain transfer appears on targeted chain

Once the leader has collected enough signatures, she can broadcast a fully signed transaction to the network and make it appear in the mempool of all the other nodes. This transaction will then be persisted in the next block of the chain B network.  
After that, each federation member monitoring the chain B will match the transaction found on chain by its inputs, outputs (including the OP_RETURN), and update the status of the transfer.

![The cross chain transfer appears on targeted chain](../assets/cross-chain-transfers/happy-path-3.svg)

### The cross chain transfer passes chain B's maximum reorg length

Finally, as chain B progresses and the transfer's maturity passes the maximum reorg length, all federation nodes monitoring the B chain can update the status of the corresponding session in their respectives stores. Once the status of the session is marked complete, no more work will be required for this session.

![The cross chain transfer matures on targeted chain](../assets/cross-chain-transfers/happy-path-4.svg)

## 2) Preferred leader offline

### A cross chain transfer is triggered while leader is offline

Similar to the previous case, deposit transactions to the federation's multisig on chain A have triggered a cross chain transfer. Here however, the prefered leader happens to be offline. All other nodes carry on as usual, creating and exchanging partially signed transactions. However, no one is here to broadcast it as FM<sub>2</sub> is offline.

![A cross chain transfer is triggered while leader is offline](../assets/cross-chain-transfers/leader-offline-1.svg)

### The cross chain is handled one block later by next leader

As time passes, a new block appears on chain A, and a new leader is elected (FM<sub>3</sub>). This new leader can now broadcast the transaction and we are back on the normal path (cf 1.).  

As described earlier every new leader must first check for previously unprocessed transfer and process those first before attending to the transfers on current block, if a few leaders are offline this can create a backlog of transfers.  
If a leader finds a previously unprocessed transfer and they dont have enough signatures to broadcast they must do nothing and wait for the other members to send over signatures.  
If not enough members are online to reach the minimum number of signature required by the multisig, then transfers will halt untill enough members come back online and broadcast signatures.

![The cross chain is handled one block later by next leader](../assets/cross-chain-transfers/leader-offline-2.svg)

### The leader comes back online

When the leader comes back online, it will retrieve from its stores what was the last known block heights for which it had dealt with transfers, and resync its store from there while synching up its nodes. Partial transactions will then be added to the store and updated (or potentially completed) depending on their status on the chain B.  
The node will only be able to participate in cross chain transfers when it is fully synced again, and has the correct view of the multisig utxos set.  

A leader that is now synced and observing unprocessed previous transfers will start creating signatures for those transfers and broadcasting them to the other federation members.

![The leader comes back online](../assets/cross-chain-transfers/leader-offline-3.svg)

## 3) Building the chain B transaction deterministically

There are two main reasons for building transactions deterministically.
1. To allow for each member to independently build the exact same transaction, and therefore ensure that the federation members will never need to sign a UTXO more than once. This is for security reasons.
2. To simplify the processes of sharing signatures (a non-deterministic approach means a leader needs to create a session for the transfer and coordinate with peers to sign it).  

Assuming members are synced and have the same view of the multisig address (the collection of UTXOs) then using a predefined algorithm every member can generate the transfer transaction, sign with their key and broadcast to the other members (possibly it's enough to only broadcast to the current leader).  
It may be that in order to use a multisig UTXO the algorithem will require it to be burried under enough blocks, to avoid reorgs and ensure that all member have the same view of the UTXO list.

**The algorithm to select UTXOs:**
- The oldest UTXO first
- If a block has more than one UTXO, then first one in the list is first
- Outputs are sorted by destination address
- Change address (back to multisig), if any, is last.
- OP_RETURN of block hash of chain the transfer came from.

A member that comes back online after a long perioud will need to sync both chains and also build the databse of transfer transactions, they should be able to do this simply by looking at the chains themselves.  
A member that came online will, once it is done syncing both its chains and has the correct views of the multisig UTXO lists, check all the unprocessed transfers, start building and signing corresponding transactions, and broadcast them to its peers. 

![Building transactions](../assets/cross-chain-transfers/building-transaction.svg)

**The algorithem to select the leader:**

TODO:
either reuse the current mechanism : hash block hash with public keys of members and order them alphabetically to get a leader table
or have the leader take turn in a pre-established way based on height of block modulo federation size (somewhat similar to POA)

## Considerations ## 

- What is the fee going to be on the transfer trx (to maintain determinism)
- What if a block is full of transfer transactions (too full for the other chain)
- What if all members are offline how do we handle resync of federation members

What can cause a member to generate a multisig that is different for the rest of the federation (break determinism)


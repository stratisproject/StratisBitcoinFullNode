# Integration test scenarios

These are a few scenarios related to sidechains that we would like to automate as integration tests, please feel free to add to this list or review the existing. Once we are happy about a scenario, we can proceed to create corresponding issues and implement them. 

## Routine federation members
-----------------------------

Tests from this section should ensure that the federation can run as specified, there might be overlapses with pure POA tests, but we can try to minimise that.

### RFM-1 - One federation member disconnect and the sidechain keeps on progressing 

1. All federation members are connected and at the top of both chains.
2. One gets disconnected and other federation members keep on as usual. POA based sidechains progress without the disconnected member.
3. The fact that the member is offline triggers a warning on all other members (to be used in dashboard).

### RFM-2 - One federation member disconnect and comes back online 

1. Federation members is connected and at the top of both chains.
2. It then gets disconnected and other federation members keep on as usual. Both chains progress without the disconnected member.
3. The federation member comes back online and resync.
4. The federation member has the correct multisig balance on both chains.

## Cross chain transfers
-----------------------------

### XCT-1 - With all members online during the transfer

1. All federation members are online and a deposit is made to their multisig address
2. After maxReorg a prefered leader is elected and all members starts creating partial transactions
3. All members should get the signatures of other members as they get broadcast, and persist them in store
4. The leader has enough signatures and puts a fully signed transaction in the mempool
5. All members notice the transaction in the mempool and update the status of the corresponding session in their individual stores
6. The transaction matching the transfer gets mined and all members update its status in their individual stores
7. After maxReorg on target chain, all members update the status of the session to completed and stop monitoring it.

### XCT-2 With leader offline

1. 1.2.3. from XCT-1
2. The leader goes offline before getting a quorum of signatures.
3. Other members get a quorum but no other member send the signed transaction to the mempool (it is not their turn yet).
4. One more block gets created on the source chain, and the next leader is selected.
5. The new leader pushes the fully signed transaction to the mempool.
6. 5.6.7. from XCT-1

### XCT-3 With all members online and the target chain gets reorged

1. 1.-6. from as XCT-1.
2. One block passes on source and target chains.
3. Another deposit happens one or two blocks later in the source chain and triggers another cross chain transfer.
4. Somehow a reorg happens _(Something probably needs to be done create a fork in the previous steps)_ol) Transactions matching deposits 1 and 2 come back to the mempool are dealt with in order by the current leader.
5. Transactions go back on chain and all members update their databases
6. After maxReorg on target chain, all members update the status of the session to completed and stop monitoring it.

### XCT-4 From the point of view of the users

NB : This could be an isolated test, but could also be something that we check on _all_ XCT-* tests.

1. Make a deposit to the multisig wallet of the federation on source chain.
2. Ensure the federation is up and running (probably no more checks than that, here it is just the user's point of view).
3. Monitor events happening in the federation, and wait for the one where it signals that a session appeared in mempool.
4. Ensure the deposited funds have appeared at the targeted address.
5. Ensure the multisig balances are in order.

### XCT-5 Federation members are mostly offline, then enough of them come back online

1. Most federation members are offline, and we shouldn't manage to get a quorum
2. A user makes a deposit on the multisig, and source chain progresses passed maxReorg.
3. The online federation members start the process of creating partially signed transactions.
4. Ensure that a block for which a leader was online passed and yet the transaction never gets to the mempool.
5. Ensure all federation members databases have a good status for the given transfer.
6. Enough federation members come back online and start synching.
7. No newly online member can do anything while they are still syncing.
8. Once members are ready, they start broadcasting the signature for the transfer session.
9. The leader is online and has enough signatures to propagate the transaction.
10. 5.6.7. from XCT-1

### XCT-6 One part of the federation goes rogue and sends bad cross chain transfers

1. 1.2. from XCT-1
2. One or more members agree on sending a different version of the transaction representing the cross chain transfer. The leader is part of them.
3. The honest part of the federation sends the correct transaction (which respects the deterministic creation process).
4. On the honest nodes, the wrong transaction is not accepted.
5. The wrong transaction does not make it to the mempool, lacking signatures.
6. On the next leader round, enough signatures have been collected from honest memebers and the transaction goes to the mempool.
7. 5.6.7. from XCT-1

## Sidechain transfers
-----------------------------

### ST-1 Standard transaction inside the sidechain

1. Put some money in a sidechain wallet.
2. Have another wallet ready to receive funds.
3. Create a sidechain transaction.
4. Ensure the funds are transfered correctly.

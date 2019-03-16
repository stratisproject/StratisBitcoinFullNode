# ProvenHeaders specifications
## Table of contents

- [Problem overview](#problem-overview)

- [Solution abstract overview](#solution-abstract-overview)

- [Basic constructs](#basic-constructs)

- [Structure of proven header](#structure-of-proven-header)

- [Assumptions](#assumptions)

- [Best chain selection](#best-chain-selection)

- [Changes required for the C# node](#changes-required-for-the-c-node)

  ​


## Problem overview

Syncing with headers is used for quick blockchain downloads. A node can ask its peers for headers using *getheaders* message and the asked peer can send up to 2000 headers per *headers* message. When the headers are received, they are used in order to ask peers for blocks those headers represent. This protocol is new and used on bitcoin network because it has some advantages in terms of resources usage over using *inv* messages.

On PoW chains, headers can't be easily faked because creating a valid header requires spending a lot of hashing power to meet the PoW difficulty target.

On PoS networks like Stratis, it is easy to construct a fake chain of headers of any length and almost arbitrary chainwork.

With our C# node implementation, if an attacker constructs a fake chain of headers that has more work than the valid chain and sends it to a node, that node will switch to the fake chain of headers. After switching, the node will ask for blocks that are represented by the fake headers but those blocks will never be received. As a result, the node will stay out of sync with the network until the moment the network eventually produces a longer chain and the node switches back.

*Therefore it is possible to perform an attack on any node that uses getheaders and keep it out of sync for a long period of time.*

## Solution abstract overview

In order to prevent attacks using fake headers, we propose the use of proven headers.

New type of network messages needs to be introduced to allow syncing using proven headers -

1.  *provhdr* - `ProvenHeadersPayload` which contains a list of up to 2000 proven headers.
2. *sendprovhdr* - `SendProvenHeadersPayload` which informs the peer that we are only willing to sync using the proven headers and not the old type of headers.
3. *getprovhdr* - `GetProvenHeadersPayload`  which requests proven headers using a similar mechanism as the `getheaders` protocol message.





## Basic constructs

#### Coinstake age

 `coinstakeAge` is a consensus constant that specifies how old (confirmation-wise) a UTXO has to be in order to be allowed to be used as a coinstake's kernel.

This means that if we are synced up to the block of height `x` and we receive a proven header that represents a block within the range from `x + 1` to `x + coinstakeAge` then we have enough data to validate the coinstake's kernel because a valid kernel couldn't have been originated in a block with height greater than `x`.

It's important to mention that other coinstake transaction data may be fake (non-kernel inputs, fees claimed) but we don't care about this since this data is not related to the chain work.



#### Trusted base

We define `trustedBase` as the block up until which the node has a UTXO set that it trusts (including that block). In the C# Stratis full node  `trustedBase`  is represented by the consensus tip, and in Stratis C# light wallet node it is currently not represented. More on how trusted base should be represented for the light wallet is in the [trusted base for the light wallet](#trusted-base-for-the-light-wallet) section.



#### Long reorg protection

PoS blockchains similar to Stratis need to have a protection against long reorganisations. Stratis protocol prevents the node from switching to another chain that would replace more than 500 blocks. We denote this parameter as `maxReorg`.



## Structure of proven header

A proven header consists of:

1. A block header (in the C# node it is represented by the `BlockHeader` class) which contains the merkle tree root.

2. A block header signature (in the C# node it is represented by the `BlockSignature` class), which is signed with the private key which corresponds to the coinstake's second output public key.

3. Its coinstake transaction (in the C# node it is represented by the `Transaction` class).

4. A merkle proof that proves the coinstake tx is included in a block that is being represented by the provided header.

   In the C# node the merkle proof can be represented by the `PartialMerkleTree` class.



## Assumptions

This document is written under the assumption that a soft fork that changes `coinstakeAge` consensus parameter from 50 to 500 is activated for Stratis.

The solution proposed in this document should be implemented for the C# nodes only when miners that control at least 51% of all stake adopt the soft fork.



#### Reasons behind changing coinstakeAge

If the `coinstakeAge < maxReorg` then it is possible to create a valid coinstake kernel which will use UTXO originated in a block within the bounds of long reorg protection. Such a coinstake can't always be validated with a small proof.

Alternative solutions with longer proofs have been explored as well but they always introduce new attack vectors and additional complexity to the code which makes those solutions inferior to just implementing syncing with the *inv* messages.




## Changes required for the C# node

In this section changes that will be required for the C# node to support syncing with *proven headers* are covered.

#### Handshake

Also after the handshake is completed *sendprovhdr* message should be sent instead of *sendheaders*.

*sendprovhdr* message is a message just as *sendheaders* is and it is just another message type to distinguish between those two. It will also contain the height from which we prefer having proven headers over normal headers.



#### Storing chain of proven headers

Currently on the C# node we have ChainRepository which stores the chain of headers. It should be extended to store the full *proven headers* chain for blocks that were created after the `activationHeight`.



Also, it will be reasonable to merge the `StakeChain` with the ChainRepository database.

#### C# nodes syncing from StratisX nodes

Syncing from StratisX nodes is currently done using *headers* message and StratisX nodes won't be updated to be able to understand *provenHeaders* messages.

However if issue [1156](https://github.com/stratisproject/StratisBitcoinFullNode/issues/1156) is resolved then we can allow using old headers messages for synchronization up to the last checkpoint. But we still need C# nodes to be able to sync from the StratisX nodes after the last checkpoint.



Before the soft fork is activated syncing after the last checkpoint from the StratisX can be done using one of the following options:

1. Allow syncing only from C# nodes or whitelisted StratisX nodes (list of those nodes can be requested from the seed nodes)- we suggest this option to be implemented.
2. Partially implement syncing using *Inv* messages without using *headers* protocol at all (this would emulate the behavior of early bitcoin nodes version).

If the maximum outbound connections limit is reached and there are less than 1 outbound connection to the C# node that supports syncing with the *proven headers*, one random connection should be dropped to allow the node to continue making connection attempts in order to find a C# node that supports *proven headers*.



#### Proven headers validation rules

1. Check if the serialized size of the proven header is less than 1 000 512 bytes

2. Check header version

3. Header should be able to connect to genesis

4. Check if difficulty target was calculated properly

5. Check if header timestamp > prev header timestamp

6. Check if it follows max reorg protection rule

7. Check timestamp <= future drift of the adjusted time

8. Check that coinstake is valid:

   1. Check that coinstake tx has `IsCoinStake` property equal to `true`
   2. Header time is equal to the timestamp of the coinstake tx
   3. Check if coinstake tx timestamp is divisible by 16 (using timestamp mask)
   4. *Verify the coinstake age requirement
   5. *Verify all coinstake transaction inputs
      1. Input comes from a UTXO
      2. Verify the `ScriptSig`
   6. *Check if the coinstake kernel hash satisfies the difficulty requirement
   7. Check that coinstake tx is in the merkle tree using the merkle proof
   8. *Verify header signature with the key from coinstake kernel




If the header is not following any of those rules it is considered to be invalid.

It's important to note that proven headers that are `coinstakeAge` blocks ahead of the `trustedBase` can't always be validated (some data might be missing) so this algorithm should not be used to validate such headers until `trustedBase` advances enough.

\* means that those rules are expensive to execute.



It might happen that when new chain of proven headers is received some of the proven headers will be referring to UTXOs that are spent on `trustedChain`. In order to validate such headers a coinview rewind is performed until the forkpoint will be required. Since this is a very expensive operation doing so will introduce a new attack vector: attacker can send us fake headers which node can validate only after a rewind.



In order to reduce expenses of validation rules execution we propose following solution:

First run the algorithm using current coinview. In case in step 8.4 or 8.5 or 8.6 or 8.8 header is being marked as invalid because the UTXO used in coinstake transaction is not found in the coinview we are rerunning those steps using coins fetched from the rewind data.

In order to achieve efficient fetching from the rewind data a new data structure should be introduced:

forkPoints data structure is a key-value storage where key is a TxId + N (N is an index of output in a transaction) and value is a rewind data index. This data structure will always contain as many entries as there are rewind data instances in the database (currently we do not delete old rewind data that is no longer needed but after the [issue #5](https://github.com/stratisproject/StratisBitcoinFullNode/issues/5) is fixed we should also make sure that old fork point data is deleted as well).

When we need to fetch a UTXO first we check if it was spent in a rewind data with index that is associated with a block created after the fork point.  If the coin was spent before the fork point then the coinstake where this coin is being used as an input is invalid. Otherwise we use the rewind data id to fetch the rewind data instance and get a UTXO from it.

forkPoints data structure should be updated every time new rewind data instance is created or deleted. Since this data structure will be very lightweight it should be kept in the RAM and persisted to the disk every time it is updated (this includes altering the fork point data starting from the fork point in case a reorg happens).


#### Trusted base for the light wallet

Stratis C# light wallet node currently does not have a representation of the trusted base so one should be implemented.

Trusted base for the light wallet can be based on UTXO set (coinview). In general the implementation will be similar to the ConsensusLoop except for the fewer validation rules in place.

When the new block is downloaded it should be validated using the set of rules that is covered in the next section. If the block violates any of those rules it should be discarded and the peer that provided it should be banned. In case the block is discarded the CoinView should be rewinded to the previous block.



##### List of rules to validate the blocks and advance with trusted base

1. Check if every single transaction that is included in the block is valid according to the following rules (it is suggested to divide those checks to expensive and inexpensive and do them separately, inexpensive first):

   1. Check tx timestamp <= header timestamp
   2. Make an iterative overflow check of all outputs and inputs (see `CheckTransaction` in C# node's code)
   3. Check that inputs list doesn't contain any duplicated UTXOs
   4. Check if the transaction is finalized
   5. If tx is coinbase it's outputs are zero
   6. If tx's input was originated in coinstake or coinbase then check the maturity
   7. Check if input tx's timestamp is older than tx timestamp
   8. Sum of all inputs has to be greater than the sum of all outputs (the difference between them is the total amount of fees that can be claimed in this block)
   9. Check if transaction fee >= minimal fee
   10. Check all inputs for a tx are valid UTXOs
   11. Verify `SciptSig` of all inputs
   12. Mark all inputs as spent in the coinview

2.  Check the merkle root

3. Check that the coinstake reward is <= fees + block subsidy

   ​

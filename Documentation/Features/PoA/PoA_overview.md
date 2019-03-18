

# Proof-of-Authority (PoA) design

## Abstract

Proof-of-Authority is consensus algorithm that can be used instead of Proof-of-Work or Proof-of-Stake.

It does not depend on nodes solving arbitrarily difficult mathematical problems, but instead uses a set of “authorities” - nodes that are explicitly allowed to create new blocks and secure the blockchain. This makes it easier to maintain a private chain and keep the block issuers accountable. Also PoA is more predictable since blocks are issued at steady time intervals.

##### Setting up a Proof of Authority network

1. Pick real world entities that should control the network, so called authorities.
2. Authorities should exchange their public keys and place them in the network configuration.
3. Each authority should run a node in order to participate in blocks creation.



## High level design overview

Federation consists of N people (best security parameters can be achieved when N is not divisible by 2: 3/5/7/9 members and so on. However, this is not enforced by PoA consensus).

Each of federation members generate a key pair and exchange their public keys. Public keys are added to the network settings and known to everyone who uses this network.

Order in which they can mine a block is predefined on the sidechain setup.

Federation members mine the blocks in predefined order and sign mined blocks with their keys. They include only those transactions that they consider to be valid.
If one of the federation members don't mine a block when it's his turn, the member after him will wait `targetSpacing` seconds and will mine the block. There is no subsidy for mining the blocks.

All nodes on the network are validating blocks and transactions normally.

However PoA approach allows light wallet to be created easily in a way that light wallet won't need to validate anything except for the block signature and timestamp.



##### Example

We have 3 federation members: A,B,C
`-` equals to `targetSpacing` seconds passed:

Normal scenario is:

`A-B-C-A-B-C-A-B...`

In case B is offline:
`A--C-A--C-A--C-A...`



There is a consensus rule for miner: you can produce a block only in your slot (in case there are 3 members it will be once in `3 * targetSpacing` seconds.



In case federation members have a disagreement and some of them include txes that other think are invalid we will have a chain split. Let's say A at some point decided that he wants to mine a fake TX, this is what will happen:

```
A-B-C--B-C--B-C--B-C
      A---A---A---A
```



A's chain will always be shorter in terms of chainwork so all the nodes on the network will prefer the best chain which is maintained by B and C.

The security model of this sidechain design relies on 51% of miners being honest. As long as it's true fake chains will always have less chainwork.

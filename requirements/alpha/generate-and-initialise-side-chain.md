# Use Case – Generate and Initialize Sidechain

## Description

The Sidechain Generator actor performs the
following steps to generate a new sidechain:

1) Generates a Blockchain using the Stratis Blockchain Generation
technology.

2) Organizes with the Federation
Members to have them generate mainchain and sidechain public/private key pairs.

3) Initializes the Sidechain (this
performs the pre-mine and mines the coins into the multi-sig address on the
sidechain).

4) Returns generated ScriptPubKeys
and multi-sig addresses to each of the federation members.

## Primary Actor

The **Sidechain Generator** actor.

## Secondary Actor

The **Federation Member** actors whom the primary actor communicates with.

## Related Use Cases

Use Case - Generate Federation Member Key
Pairs

## Preconditions

The Sidechain Generator must be running a
Stratis Full Node with the relevant extensions to support sidechain generation.

Persons who will undertake the Federation
Member actors have been chosen and the Sidechain Generator can contact those
members (eg by email).

The total number of members in the
federation is known at this time, as is the quorum number.

## Postconditions

A new sidechain is created, initialized and
running.

Coins have been safely pre mined on the
sidechain and are stored in a multi-sig address under the custodianship of the
federation.

A multi-sig address is setup on mainchain
and ready to store locked coins on the mainchain.

Each federation member has safely stored
away their keys.

The sidechain is ready and able to receive
funds from the mainchain.

## Main Success Scenario

The actor follows the steps to create a new blockchain using Stratis’ Blockchain Generation technology (described elsewhere).  

The actor communicates with each Federation Member and they follow the process described in the Generate Federation Member Key
Pairs use case.  

The actor receives all the public keys from the Federation Members.  

The actor navigates to an initialize sidechain feature. He enters the multi-sig quorum parameters (eg 12 of 20) and enters the folder location (federation folder) where the collected public keys are located.  

The actor issues the Initialize Sidechain Command. The sidechain is initialized.  

The actor receives the ScriptPubKey (redeem script) files for each Federated Member. A public address is also generated. These files must be sent back to the federation members to store with their important sidechain key files. There are four of these small files in total since a redeem script and an address are required for both sidechain and mainchain. In addition the public key of each member
is distributed back to all federation members.

The use case ends.

## Extensions

None

## Frequency of Use

Once at the start of the sidechain creation
process.

## Other Consideration

It would be possible to make install packages at this stage that are preconfigured to run the created sidechain and have the public address of the federation set as default.  

Although orchestration of this process could be conducted by console apps it would be far easier if a web application could be used (however currently only a console app is in scope).  

We may also want to store node seed addresses of federation nodes in the packages.  

The pre-mine and multi-sig storage of coins could be enforced by consensus for greater security.  

Although input is described above for multi-sig N and M parameters and the sidechain folder location, it is more likely that this will be configuration rather than any kind of formal input.  
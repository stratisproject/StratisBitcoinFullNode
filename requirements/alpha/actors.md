# Actors

## Sidechain Funder
The Sidechain Funder actor can send coins from the mainchain to the sidechain (deposit transaction) or receive coins from the sidechain back to mainchain (withdrawal transaction). 

### Primary use cases:
- Deposit Funds to Sidechain
- Withdraw Funds from Sidechain

## Sidechain Generator
The Sidechain Generator actor is a technical user who sets up and configures the sidechain.  He creates the sidechain using Stratis’ Blockchain Generation technologies. He then initializes the sidechain which pre-mine coins into a multi-sig address.  The Sidechain Generator may very well be a member of the Stratis Consulting team.
### Primary Use Cases:
- Generate and Initialize Sidechain

## Federation Member
The Federation Member actor is a member of the sidechain Federation and therefore holds private keys used to sign multi-sig transactions. They hold one mainchain and one sidechain key.  The Federation members collectively become custodians of locked funds on the mainchain and distribute coins on the sidechain. They also authorize any withdrawals.  
### Primary Use Cases:
- Generate Federation Member Key Pairs

## Sidechain Asset Holder
The Sidechain Asset Holder actor is a standard user who uses the sidechain for some purpose and has acquired ownership of coins on the chain. He may also act as a Sidechain Funder to deposit or withdraw funds to/from the sidechain.

## Federation Gateway Node
This special actor is a system node running a both the sidechain and the mainchan with the Federation Gateway Feature added.  The nodes monitor the two chains for Deposit and Withdrawals and coordinate the signing of transactions from/to the multisig wallets.
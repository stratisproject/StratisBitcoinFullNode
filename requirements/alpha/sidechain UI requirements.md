# Sidechains User Interface Changes

## Requirements
The wallet UI should be able to be configured to both start up its node or to connect to an existing running node.  
The wallet UI needs to be able to connect to the sidechain port which is configurable. Future sidechains will use different ports.  
The wallet UI should only present sidechain options when appropriate and sidechain features should not get in the way of a normal user who is using the wallet for Bitcoin or Strat only and not using a sidechain.  
When running and connecting to a sidechain any labels and iconigraphy should be changed to the sidechain information (chain name, currency symbols, strat symbols etc).  

## New Dialogs
We require two new dialogs that are similar to the standard Send Dialog.  Deposit and Withdrawal.  The Deposit dialog only runs on Mainchain and the Withdrawal dialog only on a Sidechain.
Both have standard features of the send dialog:
- Ability to specify an amount.
- Ability to specify a destination address.
- Choose fee type (low medium high).
- Enter wallet password to send.

However the following aspects are different:
- The normal wallet destination address is now a multi-sig address and only particular address is valid for a sidechain. The address is different in the Deposit and Withdrawal dialog boxes.
- An additional destination address is specified which is first looked up using the counter chain wallet.  

These aspects need to be designed carefully so that the user knows exactly what he needs to put where as getting it wrong can result in loss of funds.

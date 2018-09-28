# Use Case – Withdraw Funds from Sidechain

## Description
The Sidechain Funder actor performs a withdrawal transaction to send funds from the sidechain to the mainchain.  
_[note: This process is a mirror image of the Deposit.  Here the Sidechain Funder sends funds to a mainchain address. Both Withdrawal and Deposit features are accessed through the sidechain wallet UI.]_

## Primary Actor
The Sidechain Funder actor.

## Secondary Actor
none

## Related Use Cases
Deposit Funds to Sidechain.

## Preconditions
The actor has funds on the sidechain.  
The actor must be running both mainchain and sidechain nodes locally. These can be full or light nodes.  
The actor must have received in advance the sidechain Multi-Sig Federation Address to which he will send his funds.

## Postconditions
The actor sees that his balance on the sidechain has decreased and that he has now received funds on mainchain.

## Main Success Scenario
The actor navigates to his Mainchain wallet and issues the Receive command. The wallet displays a Mainchain Destination Address which he can copy.  
The actor navigates to his Sidechain wallet and issues the command to Withdraw Funds from Sidechain.  
The actor enters the Mainchain Destination Address that he copied in the step above. He then also enters the Sidechain Multi-Sig Federation Address that he obtained previously.  
The actor issues the command to Send the transaction and the wallet confirms and broadcasts the transaction in the normal manner.  
The actor waits for a period of time (perhaps 100 blocks) and will see funds appear in his Mainchain wallet after that period of time has elapsed.
The use case ends.

## Extensions
Should funds be rejected by the Federation they are returned to the actor (let transaction fees). They will appear in the sidechain wallet after a period of time. Refunds are a manual process.

## Frequency of Use
Rarely on some chains that may fund only once or twice.  
Other setups may allow regular withdrawals – perhaps up to several per day per user.

## Other Consideration
Node that at least one Federation Node will need to be in operation before this can work.  
It may be possible to ‘hard code’ a default federation address in to the wallet somehow.


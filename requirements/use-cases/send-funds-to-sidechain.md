# Use Case – Deposit Funds to Sidechain

## Description
The Sidechain Funder actor performs a deposit transaction to send funds from the mainchain to the sidechain.

## Primary Actor
The Sidechain Funder actor.

## Secondary Actor
none

## Related Use Cases
Withdraw Funds from Sidechain.

## Preconditions
The actor has funds on the mainchain.  
The actor must be running both mainchain and sidechain nodes locally. These can be full or light nodes.  
The actor must have received in advance the Multi-Sig Federation Address to which he will send his funds.

## Postconditions
The actor sees that his balance on mainchain has decreased and that he has now received funds on the sidechain.

## Main Success Scenario
The actor navigates to his Sidechain wallet and issues the Receive command. The wallet displays a Sidechain Destination Address which he can copy.  
The actor navigates to his Mainchain wallet and issues the command to Send Funds to Sidechain.  The actor views the name of the chain and therefore can verify that he is sending to the correct chain. The actor enters the Sidechain Destination Address that he copied in the step above.  He then also enters the Multi-Sig Federation Address that he obtained previously.  
The actor issues the command to Send the transaction and the wallet confirms and broadcasts the transaction in the normal manner.  
The actor waits for a period of time (perhaps 100 blocks) and will see funds appear in his Sidechain wallet after that period of time has elapsed.  
The use case ends.

## Extensions
Should funds be rejected by the Federation they are returned to the actor (let transaction fees). They will appear in the mainchain wallet after a period of time. Refunds are a manual process.

## Frequency of Use
Rarely on some chains that may fund only once or twice.
Other setups may allow regular deposits – perhaps up to several per day per user.

## Other Consideration
Node that at least one Federation Node will need to be in operation before this can work.
It may be possible to ‘hard code’ a default federation address in to the wallet somehow.


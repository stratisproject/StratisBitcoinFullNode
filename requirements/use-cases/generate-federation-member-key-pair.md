# Use Case – Generate Federation Member Key Pairs

##Description
The Federation Member actor creates their mainchain and sidechain private keys and sends their public keys to the Sidechain Generator actor. The Sidechain Generator receives two public keys, one for mainchain and one for the sidechain.  
These generated keys are used primarily to create an address where funds can be deposited into the multi-sig address.  The private keys are also used to sign transactions that move funds out of the multi-sig when a quorum of federation members approve the transactions.  
The public keys are also used in some scenarios to distinguish otherwise identical Federation Gateways.  One such scenario is when federation gateways take turns to build and broadcast a transaction.  If the chosen node does not perform its duty within a time frame (currently 5 mins) then another gateway is chosen.

## Primary Actor
The Federation Member actor.

## Secondary Actor
The Sidechain Generator actor.

## Related Use Cases
The Generate and Initialize Sidechain

## Preconditions
The actor has agreed that he will take part in forming a Federation that has the collective responsibility of managing deposits and withdrawals to and from a sidechain.  
The actor has a channel to communicate with the Sidechain Generator actor (see other considerations).  
There is no requirement to run a node to perform this operation.  

## Postconditions
The Federation Member has safely stored away the following:
a) A private key for mainchain
b) A private key for the sidechain
c) A public key for mainchain
d) A public key for the sidechain
e) A ScriptPubKey and Address for mainchain
f) A ScriptPubKey and Address for the sidechain.

## Main Success Scenario
The Federation Member actor navigates to an application and issues a command to Generate Federation Key Pairs.  
The actor enters their full name.  
The actor enters a Password and asked to confirm it.  
The actor is reminded to not forget or share his password.  
The actor issues the generate command.  Text files are produced and it is made absolutely clear once again that the user is not to lose his password and to take care of the files.  
The actor communicates the public keys with the Sidechain Generator.  
The Sidechain Generator sends back two ScriptPubKeys and derived addresses that must also be stored securely with the other files.  
The Sidechain Generator also sends back all member public keys to all federation members.  
The use case ends.

# Extensions
none

## Frequency of Use
This happens only once when the sidechain is first created.

## Other Consideration
Only the Federation Member themselves get to see the private key and they must keep it safe and not share it with anyone.  
The application used to generate the keys could be a web application or a console app. (currently only the console app is in scope)  
The generated public keys need to be communicated back to the Sidechain Generator actor. This could be done by email after the console app is used.  This communication channel does not need to be secure because a public key is sent.  
The public key data file must include the name of the Federation Member so that the Sidechain Generator can track who he has received keys from.  
There are two sets of public keys generated.  One for mainchain and one for the sidechain.  
Although it is thought that the Federation Gateway Operator and the Federation Member roles are fulfilled by the same person, this may not always be the case.  In practice a Federation Member can delegate the ‘IT tasks’ to another person.  This division of labor necessitates that the Federation Member then share his Private Key with the Federation Gateway Operator and this is a suboptimal solution. (reference: MasterNode project where the same division of labor happened and the MasterNode owner complained that he needed to share his private key with his IT guy.)

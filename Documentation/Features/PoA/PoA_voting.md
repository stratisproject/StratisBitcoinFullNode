### PoA Voting

Voting is a feature that allows federation members to vote on something. At the moment of this document creation federation members can vote to add a new federation member, remove federation member, add hash to whitelisted hashes store or remove hash from there. Whitelisted hashes are being interpreted by other components. For example smart contracts feature uses whitelisted hashes as hashes of smart contracts allowed on the network. 

Voting can be enabled or disabled by setting bool variable `PoAConsensusOptions.VotingEnabled` during network setup.



Voting works like that: 

1. Federation member calls API that constructs pending voting data 
2. When federation member mines a block voting data is included in the special output in coinbase transaction
3. Other federation members do the same and when 51% of fed members voted poll's execution is scheduled (this will happen after max reorg blocks)
4. Change is applied



### Api endpoints

- `getfedmembers` - returns a list of all active federation members. 
- `getpendingpolls` - returns a list of all polls that are not executed yet and doesn't have >=51% of fed members voted in favor.
- `getfinishedpolls` - returns a list of polls which were already voted for by >=51% of federation members.
- `schedulevote_addfedmember` - votes in favor of adding a new federation member.
- `schedulevote_kickfedmember` -  votes in favor of removing existing federation member.
- `getwhitelistedhashes` - provides a list of whitelisted hashes. 
- `schedulevote_whitelisthash` -  votes in favor of whitelisting a new hash.
- `schedulevote_removehash` - votes in favor of removing existing whitelisted hash.
- `getscheduledvotes` - provides a list of all votes that will be included in the next block federation member mines.




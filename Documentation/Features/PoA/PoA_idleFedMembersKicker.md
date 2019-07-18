### Idle Federation Members Kicker

This feature kicks federation members where they have not been active for a prolonged period of time. 

To enable it you need to configure the network appropriately:

First of all voting the feature should be enabled because kicking is done via a voting process among all federation members. Kicking a member requires a majority of votes in favor of kicking a member. To enable voting you need to set `PoAConsensusOptions.VotingEnabled` to `true`.

To enable kicking of idle fed members you need to set `PoAConsensusOptions.AutoKickIdleMembers` to `true` and optionally change `PoAConsensusOptions.FederationMemberMaxIdleTimeSeconds`.  `FederationMemberMaxIdleTimeSeconds` is the time that the federation member has to be idle to be kicked by others. By default this is set to 7 days.

The component that implements the kicking functionality is `IdleFederationMembersKicker`.

Federation member's inactive time is counted from the moment the last block was produced by that particular federation member or, in the case where the federation member never produced a single block, time is counted from when the first block after genesis was mined.


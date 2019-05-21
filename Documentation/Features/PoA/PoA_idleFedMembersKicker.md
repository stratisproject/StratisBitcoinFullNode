### Idle Federation Members Kicker

This feature kicks federation members in case they were not active for a prolonged period of time. 

To enable it you need to configure the network appropriately:

First of all voting feature should be enabled because kicking is done via voting process among all federation members, kicking a member requires a majority of votes in favor of kicking a fed member. To enable voting you need to set `PoAConsensusOptions.VotingEnabled` to `true`.

To enable kicking of idle fed members you need to set `PoAConsensusOptions.AutoKickIdleMembers` to `true` and optionally change `PoAConsensusOptions.FederationMemberMaxIdleTimeSeconds`.  `FederationMemberMaxIdleTimeSeconds` is the time that federation member has to be idle to be kicked by others. By default it is set to 7 days.

Component that implements kicking functionality is `IdleFederationMembersKicker`.

Federation member's inactive time is counter from the moment last block was produced by that federation member or, in case federation member never produced a single block time is counted from the time when first block after genesis was mined.


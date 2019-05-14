using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    [Route("api/[controller]")]
    public class FederationVotingController : Controller
    {
        protected readonly IFederationManager fedManager;

        protected readonly VotingManager votingManager;

        protected readonly Network network;

        protected readonly ILogger logger;

        public FederationVotingController(IFederationManager fedManager, ILoggerFactory loggerFactory, VotingManager votingManager, Network network)
        {
            this.fedManager = fedManager;
            this.votingManager = votingManager;
            this.network = network;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [Route("schedulevote-addfedmember")]
        [HttpPost]
        public IActionResult VoteAddFedMember([FromBody]HexPubKeyModel request)
        {
            return this.VoteAddKickFedMember(request, true);
        }

        [Route("schedulevote-kickfedmember")]
        [HttpPost]
        public IActionResult VoteKickFedMember([FromBody]HexPubKeyModel request)
        {
            return this.VoteAddKickFedMember(request, false);
        }

        private IActionResult VoteAddKickFedMember(HexPubKeyModel request, bool addMember)
        {
            Guard.NotNull(request, nameof(request));

            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            if (!this.fedManager.IsFederationMember)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Only federation members can vote", string.Empty);

            try
            {
                var key = new PubKey(request.PubKeyHex);

                IFederationMember federationMember = new FederationMember(key);
                byte[] fedMemberBytes = (this.network.Consensus.ConsensusFactory as PoAConsensusFactory).SerializeFederationMember(federationMember);

                this.votingManager.ScheduleVote(new VotingData()
                {
                    Key = addMember ? VoteKey.AddFederationMember : VoteKey.KickFederationMember,
                    Data = fedMemberBytes
                });

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem executing a command.", e.ToString());
            }
        }
    }
}

using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Features.FederatedPeg.Controllers
{
    [Route("api/[controller]")]
    public class CollateralVotingController : Controller
    {
        protected readonly IFederationManager fedManager;

        protected readonly VotingManager votingManager;

        protected readonly Network network;

        protected readonly ILogger logger;

        public CollateralVotingController(IFederationManager fedManager, ILoggerFactory loggerFactory, VotingManager votingManager, Network network)
        {
            this.fedManager = fedManager;
            this.votingManager = votingManager;
            this.network = network;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [Route("schedulevote-addfedmember")]
        [HttpPost]
        public IActionResult VoteAddFedMember([FromBody]CollateralFederationMemberModel request)
        {
            return this.VoteAddKickFedMember(request, true);
        }

        [Route("schedulevote-kickfedmember")]
        [HttpPost]
        public IActionResult VoteKickFedMember([FromBody]CollateralFederationMemberModel request)
        {
            return this.VoteAddKickFedMember(request, false);
        }

        private IActionResult VoteAddKickFedMember(CollateralFederationMemberModel request, bool addMember)
        {
            Guard.NotNull(request, nameof(request));

            // TODO remove this line when multisig recreation is implemented.
            return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Disabled in current version.", string.Empty);

            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            if (!this.fedManager.IsFederationMember)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Only federation members can vote", string.Empty);

            try
            {
                var key = new PubKey(request.PubKeyHex);

                IFederationMember federationMember = new CollateralFederationMember(key, new Money(request.CollateralAmountSatoshis), request.CollateralMainchainAddress);

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

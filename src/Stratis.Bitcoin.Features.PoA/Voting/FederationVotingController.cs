using System;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    [ApiVersion("1")]
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

        /// <summary>
        /// Votes to add a federation member.
        /// </summary>
        /// <param name="request">Request containing member public key</param>
        /// <returns>The HTTP response</returns>
        /// <response code="200">Voted to add member</response>
        /// <response code="400">Invalid request, node is not a federation member, or an unexpected exception occurred</response>
        /// <response code="500">The request is null</response>
        [Route("schedulevote-addfedmember")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public IActionResult VoteAddFedMember([FromBody]HexPubKeyModel request)
        {
            return this.VoteAddKickFedMember(request, true);
        }

        /// <summary>
        /// Votes to kick a federation member.
        /// </summary>
        /// <param name="request">Request containing member public key</param>
        /// <returns>The HTTP response</returns>
        /// <response code="200">Voted to kick member</response>
        /// <response code="400">Invalid request, node is not a federation member, or an unexpected exception occurred</response>
        /// <response code="500">The request is null</response>
        [Route("schedulevote-kickfedmember")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public IActionResult VoteKickFedMember([FromBody]HexPubKeyModel request)
        {
            return this.VoteAddKickFedMember(request, false);
        }

        public static bool IsMultisigMember(Network network, PubKey pubKey)
        {
            var options = (PoAConsensusOptions)network.Consensus.Options;
            return options.GenesisFederationMembers
                .Where(m => m is CollateralFederationMember cm && cm.IsMultisigMember)
                .Any(m => m.PubKey == pubKey);
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

                if (IsMultisigMember(this.network, key))
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Multisig members can't be voted on", string.Empty);

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    [Route("api/[controller]")]
    public class VotingController : Controller
    {
        private readonly FederationManager fedManager;

        private readonly VotingManager votingManager;

        private readonly WhitelistedHashesRepository whitelistedHashesRepository;

        private readonly ILogger logger;

        public VotingController(FederationManager fedManager, ILoggerFactory loggerFactory, VotingManager votingManager,
            WhitelistedHashesRepository whitelistedHashesRepository)
        {
            this.fedManager = fedManager;
            this.votingManager = votingManager;
            this.whitelistedHashesRepository = whitelistedHashesRepository;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [Route("getfedmembers")]
        [HttpGet]
        public IActionResult GetFederationMembers()
        {
            try
            {
                List<string> hexList = this.fedManager.GetFederationMembers().Select(x => x.ToHex()).ToList();

                return this.Json(hexList);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("getpendingpolls")]
        [HttpGet]
        public IActionResult GetPendingPolls()
        {
            try
            {
                string polls = string.Join(Environment.NewLine, this.votingManager.GetPendingPolls().Select(x => x.ToString()).ToList());

                return this.Ok(polls);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("getfinishedpolls")]
        [HttpGet]
        public IActionResult GetFinishedPolls()
        {
            try
            {
                string polls = string.Join(Environment.NewLine, this.votingManager.GetFinishedPolls().Select(x => x.ToString()).ToList());

                return this.Ok(polls);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("schedulevote_addfedmember")]
        [HttpPost]
        public IActionResult VoteAddFedMember([FromBody]HexPubKeyModel request)
        {
            return this.VoteAddKickFedMember(request, true);
        }

        [Route("schedulevote_kickfedmember")]
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

                this.votingManager.ScheduleVote(new VotingData()
                {
                    Key = addMember ? VoteKey.AddFederationMember : VoteKey.KickFederationMember,
                    Data = key.ToBytes()
                });

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem executing a command.", e.ToString());
            }
        }

        [Route("getwhitelistedhashes")]
        [HttpGet]
        public IActionResult GetWhitelistedHashes()
        {
            try
            {
                string hashes = string.Join(Environment.NewLine, this.whitelistedHashesRepository.GetHashes().Select(x => x.ToString()).ToList());

                return this.Ok(hashes);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("schedulevote_whitelisthash")]
        [HttpPost]
        public IActionResult VoteWhitelistHash([FromBody]HashModel request)
        {
            return this.VoteWhitelistRemoveHashMember(request, true);
        }

        [Route("schedulevote_removehash")]
        [HttpPost]
        public IActionResult VoteRemoveHash([FromBody]HashModel request)
        {
            return this.VoteWhitelistRemoveHashMember(request, false);
        }

        private IActionResult VoteWhitelistRemoveHashMember(HashModel request, bool whitelist)
        {
            Guard.NotNull(request, nameof(request));

            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            if (!this.fedManager.IsFederationMember)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Only federation members can vote", string.Empty);

            try
            {
                var hash = new uint256(request.Hash);

                this.votingManager.ScheduleVote(new VotingData()
                {
                    Key = whitelist ? VoteKey.WhitelistHash : VoteKey.RemoveHash,
                    Data = hash.ToBytes()
                });

                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem executing a command.", e.ToString());
            }
        }

        [Route("getscheduledvotes")]
        [HttpGet]
        public IActionResult GetScheduledVotes()
        {
            try
            {
                List<string> votes = this.votingManager.GetScheduledVotes().Select(x => x.Key.ToString()).ToList();

                return this.Json(votes);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}

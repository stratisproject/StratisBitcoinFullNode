using System;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public interface IPollResultExecutor
    {
        /// <summary>Applies effect of <see cref="VotingData"/>.</summary>
        void ApplyChange(VotingData data);

        /// <summary>Reverts effect of <see cref="VotingData"/>.</summary>
        void RevertChange(VotingData data);

        /// <summary>Converts <see cref="VotingData"/> to a human readable format.</summary>
        string ConvertToString(VotingData data);
    }

    public class PollResultExecutor : IPollResultExecutor
    {
        private readonly IFederationManager federationManager;

        private readonly IWhitelistedHashesRepository whitelistedHashesRepository;

        private readonly PoAConsensusFactory consensusFactory;

        private readonly ILogger logger;

        public PollResultExecutor(IFederationManager federationManager, ILoggerFactory loggerFactory, IWhitelistedHashesRepository whitelistedHashesRepository, Network network)
        {
            this.federationManager = federationManager;
            this.whitelistedHashesRepository = whitelistedHashesRepository;
            this.consensusFactory = network.Consensus.ConsensusFactory as PoAConsensusFactory;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void ApplyChange(VotingData data)
        {
            switch (data.Key)
            {
                case VoteKey.AddFederationMember:
                    this.AddFederationMember(data.Data);
                    break;

                case VoteKey.KickFederationMember:
                    this.RemoveFederationMember(data.Data);
                    break;

                case VoteKey.WhitelistHash:
                    this.AddHash(data.Data);
                    break;

                case VoteKey.RemoveHash:
                    this.RemoveHash(data.Data);
                    break;
            }
        }

        /// <inheritdoc />
        public void RevertChange(VotingData data)
        {
            switch (data.Key)
            {
                case VoteKey.AddFederationMember:
                    this.RemoveFederationMember(data.Data);
                    break;

                case VoteKey.KickFederationMember:
                    this.AddFederationMember(data.Data);
                    break;

                case VoteKey.WhitelistHash:
                    this.RemoveHash(data.Data);
                    break;

                case VoteKey.RemoveHash:
                    this.AddHash(data.Data);
                    break;
            }
        }

        /// <inheritdoc />
        public string ConvertToString(VotingData data)
        {
            string action = $"Action:'{data.Key}'";

            switch (data.Key)
            {
                case VoteKey.AddFederationMember:
                case VoteKey.KickFederationMember:
                    IFederationMember federationMember = this.consensusFactory.DeserializeFederationMember(data.Data);
                    return $"{action},FederationMember:'{federationMember}'";

                case VoteKey.WhitelistHash:
                case VoteKey.RemoveHash:
                    var hash = new uint256(data.Data);
                    return $"{action},Hash:'{hash}'";
            }

            return "unknown (not supported voting data key)";
        }

        public void AddFederationMember(byte[] federationMemberBytes)
        {
            IFederationMember federationMember = this.consensusFactory.DeserializeFederationMember(federationMemberBytes);

            this.logger.LogInformation("Adding new fed member: '{0}'.", federationMember);
            this.federationManager.AddFederationMember(federationMember);
        }

        public void RemoveFederationMember(byte[] federationMemberBytes)
        {
            IFederationMember federationMember = this.consensusFactory.DeserializeFederationMember(federationMemberBytes);

            this.logger.LogInformation("Kicking fed member: '{0}'.", federationMember);
            this.federationManager.RemoveFederationMember(federationMember);
        }

        private void AddHash(byte[] hashBytes)
        {
            try
            {
                var hash = new uint256(hashBytes);

                this.whitelistedHashesRepository.AddHash(hash);
            }
            catch (FormatException e)
            {
                this.logger.LogWarning("Hash had incorrect format: '{0}'.", e.ToString());
            }
        }

        private void RemoveHash(byte[] hashBytes)
        {
            try
            {
                var hash = new uint256(hashBytes);

                this.whitelistedHashesRepository.RemoveHash(hash);
            }
            catch (FormatException e)
            {
                this.logger.LogWarning("Hash had incorrect format: '{0}'.", e.ToString());
            }
        }
    }
}

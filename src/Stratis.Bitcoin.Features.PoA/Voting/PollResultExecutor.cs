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
    }

    public class PollResultExecutor : IPollResultExecutor
    {
        private readonly FederationManager federationManager;

        private readonly WhitelistedHashesRepository whitelistedHashesRepository;

        private readonly ILogger logger;

        public PollResultExecutor(FederationManager federationManager, ILoggerFactory loggerFactory, WhitelistedHashesRepository whitelistedHashesRepository)
        {
            this.federationManager = federationManager;
            this.whitelistedHashesRepository = whitelistedHashesRepository;

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

        private void AddFederationMember(byte[] pubKeyBytes)
        {
            var key = new PubKey(pubKeyBytes);

            this.logger.LogInformation("Adding new fed member: '{0}'.", key.ToHex());
            this.federationManager.AddFederationMember(key);
        }

        private void RemoveFederationMember(byte[] pubKeyBytes)
        {
            var key = new PubKey(pubKeyBytes);

            this.logger.LogInformation("Kicking fed member: '{0}'.", key.ToHex());
            this.federationManager.RemoveFederationMember(key);
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

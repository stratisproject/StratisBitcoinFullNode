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

        private readonly ILogger logger;

        public PollResultExecutor(FederationManager federationManager, ILoggerFactory loggerFactory)
        {
            this.federationManager = federationManager;
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
    }
}

using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public interface IPollResultExecutor
    {
        void ApplyChange(VotingData data);

        void RevertChange(VotingData data);
    }

    public class PollResultExecutor : IPollResultExecutor
    {
        private readonly FederationManager federationManager;

        public PollResultExecutor(FederationManager federationManager)
        {
            this.federationManager = federationManager;
        }

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

            this.federationManager.AddFederationMember(key);
        }

        private void RemoveFederationMember(byte[] pubKeyBytes)
        {
            var key = new PubKey(pubKeyBytes);

            this.federationManager.RemoveFederationMember(key);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public interface IPollResultExecutor
    {
        void ApplyChange(VotingData data);

        void RevertChange(VotingData data);
    }

    public class PollResultExecutor : IPollResultExecutor
    {
        public PollResultExecutor()
        {

        }

        public void ApplyChange(VotingData data)
        {
            // TODO
        }

        public void RevertChange(VotingData data)
        {
            // TODO
        }
    }
}

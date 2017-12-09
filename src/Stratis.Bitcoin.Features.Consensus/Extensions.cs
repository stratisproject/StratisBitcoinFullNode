using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Features.Consensus
{
    public static class Extensions
    {
        public static ChainedBlock FindFork(this ChainedBlock newTip, ChainedBlock tip)
        {
            ChainedBlock highest = newTip.Height > tip.Height ? newTip : tip;
            ChainedBlock lowest = highest == newTip ? tip : newTip;

            highest = highest.GetAncestor(lowest.Height);

            while (lowest.HashBlock != highest.HashBlock)
            {
                lowest = lowest.Previous;
                highest = highest.Previous;
            }

            return highest;
        }

        public static async Task<bool> IfPayloadIsAsync<TPayload>(this Message message, Func<TPayload, Task> action) where TPayload : Payload
        {
            TPayload payload = message.Payload as TPayload;

            if (payload == null)
                return await Task.FromResult(false);

            await action(payload).ConfigureAwait(false);

            return await Task.FromResult(true);
        }
    }
}

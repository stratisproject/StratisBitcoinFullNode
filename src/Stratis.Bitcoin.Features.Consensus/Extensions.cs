using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public static class Extensions
    {
        public static async Task<bool> IfPayloadIsAsync<TPayload>(this Message message, Func<TPayload, Task> action) where TPayload : Payload
        {
            var payload = message.Payload as TPayload;

            if (payload == null)
                return await Task.FromResult(false);

            await action(payload).ConfigureAwait(false);

            return await Task.FromResult(true);
        }

        public static long GetSizeInBytes(this UnspentOutputs unspentOutputs)
        {
            if (unspentOutputs == null) return 0;

            return
                sizeof(uint) + // unspentOutputs.Height
                sizeof(uint) + // unspentOutputs.Version
                sizeof(uint) + // unspentOutputs.Time
                sizeof(int) + // unspentOutputs.UnspentCount
                sizeof(bool) + // unspentOutputs.IsCoinbase
                sizeof(bool) + // unspentOutputs.IsCoinstake
                sizeof(bool) + // unspentOutputs.IsFull
                sizeof(bool) + // unspentOutputs.IsPrunable
                unspentOutputs.TransactionId.Size + // uint256 - unspentOutputs.TransactionId
                (unspentOutputs.Outputs?.Sum(o => o.GetSizeInBytes()) ?? 0); // unspentOutputs.Outputs
        }

        public static long GetSizeInBytes(this TxOut txOut)
        {
            if (txOut == null) return 0;

            long scriptSize = txOut.ScriptPubKey.Length;

            // size of the script + size of all static properties (there is only one)
            return scriptSize + txOut.Value.GetSizeInBytes();
        }

        public static long GetSizeInBytes(this Money money)
        {
            if (money == null) return 0;

            // This type only has 1 field money.Satoshi, which is long
            return sizeof(long);
        }
    }
}

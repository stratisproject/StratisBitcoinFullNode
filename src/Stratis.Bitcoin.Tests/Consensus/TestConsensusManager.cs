using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class TestConsensusManager
    {
        public readonly ConsensusManager ConsensusManager;

        public TestConsensusManager(ConsensusManager consensusManager)
        {
            this.ConsensusManager = consensusManager;
        }

        public bool PeerIsKnown(int peerId)
        {
            return this.ConsensusManager.GetPeersByPeerId().ContainsKey(peerId);
        }

        public long GetExpectedBlockDataBytes()
        {
            return this.ConsensusManager.GetExpectedBlockDataBytesValue();
        }

        public void SetExpectedBlockDataBytes(long val)
        {
            this.ConsensusManager.SetExpectedBlockDataBytesValue(val);
        }

        public Dictionary<uint256, long> GetExpectedBlockSizes()
        {
            return this.ConsensusManager.GetExpectedBlockSizesValue();
        }

        public void SetMaxUnconsumedBlocksDataBytes(long newSize)
        {
            this.ConsensusManager.SetMaxUnconsumedBlocksDataBytesValue(newSize);
        }

        public void SetupCallbackByBlocksRequestedHash(uint256 hash, params OnBlockDownloadedCallback[] callbacks)
        {
            if (this.ConsensusManager.GetCallbacksByBlocksRequestedHash().ContainsKey(hash))
            {
                ConsensusManager.DownloadedCallbacks callbacksItem = this.ConsensusManager.GetCallbacksByBlocksRequestedHash()[hash];

                if (callbacksItem.Callbacks == null)
                    this.ConsensusManager.GetCallbacksByBlocksRequestedHash()[hash].Callbacks = callbacks.ToList();
                else
                    this.ConsensusManager.GetCallbacksByBlocksRequestedHash()[hash].Callbacks.AddRange(callbacks);
            }
            else
            {
                this.ConsensusManager.GetCallbacksByBlocksRequestedHash().Add(hash, new ConsensusManager.DownloadedCallbacks() { Callbacks = callbacks.ToList() });
            }
        }

        public bool CallbacksByBlocksRequestedHashContainsKeyForHash(uint256 hash)
        {
            return this.ConsensusManager.GetCallbacksByBlocksRequestedHash().ContainsKey(hash);
        }

        public void AddExpectedBlockSize(uint256 key, long size)
        {
            this.ConsensusManager.GetExpectedBlockSizesValue().Add(key, size);
        }

        public void ClearExpectedBlockSizes()
        {
            this.ConsensusManager.GetExpectedBlockSizesValue().Clear();
        }
    }
}

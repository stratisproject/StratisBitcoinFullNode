using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    // Bitcoin Core 0.17.0
    //{
    //  "chain": "xxxx",              (string) current network name as defined in BIP70(main, test, regtest)
    // * "blocks": xxxxxx,             (numeric) the current number of blocks processed in the server
    // * "headers": xxxxxx,            (numeric) the current number of headers we have validated
    //  "bestblockhash": "...",       (string) the hash of the currently best block
    //  "difficulty": xxxxxx,         (numeric) the current difficulty
    // * "mediantime": xxxxxx,         (numeric) median time for the current best block
    // * "verificationprogress": xxxx, (numeric) estimate of verification progress[0..1]
    // * "initialblockdownload": xxxx, (bool) (debug information) estimate of whether this node is in Initial Block Download mode.
    //  "chainwork": "xxxx"           (string) total amount of work in active chain, in hexadecimal
    //  "size_on_disk": xxxxxx, (numeric) the estimated size of the block and undo files on disk
    //  "pruned": xx, (boolean) if the blocks are subject to pruning
    //  "pruneheight": xxxxxx, (numeric) lowest-height complete block stored (only present if pruning is enabled)
    //  "automatic_pruning": xx,      (boolean) whether automatic pruning is enabled (only present if pruning is enabled)
    //  "prune_target_size": xxxxxx,  (numeric) the target size used by pruning (only present if automatic pruning is enabled)
    //  "softforks": [                (array) status of softforks in progress
    //     {
    //        "id": "xxxx",           (string) name of softfork
    //        "version": xx,          (numeric) block version
    //        "reject": {             (object) progress toward rejecting pre-softfork blocks
    //           "status": xx,        (boolean) true if threshold reached
    //        },
    //     }, ...
    //  ],
    //  "bip9_softforks": {           (object) status of BIP9 softforks in progress
    //     "xxxx" : {                 (string) name of the softfork
    //        "status": "xxxx",       (string) one of "defined", "started", "locked_in", "active", "failed"
    //        "bit": xx,              (numeric) the bit(0-28) in the block version field used to signal this softfork (only for "started" status)
    //        "startTime": xx,        (numeric) the minimum median time past of a block at which the bit gains its meaning
    //        "timeout": xx,          (numeric) the median time past of a block at which the deployment is considered failed if not yet locked in
    //        "since": xx,            (numeric) height of the first block to which the status applies
    //        "statistics": {
    //    (object)numeric statistics about BIP9 signalling for a softfork (only for "started" status)
    //            "period": xx,        (numeric)the length in blocks of the BIP9 signalling period
    //           "threshold": xx,     (numeric)the number of blocks with the version bit set required to activate the feature
    //           "elapsed": xx,       (numeric)the number of blocks elapsed since the beginning of the current period
    //           "count": xx,         (numeric)the number of blocks with the version bit set in the current period
    //           "possible": xx(boolean) returns false if there are not enough blocks left in this period to pass activation threshold
    //        }
    //      }
    //  }
    //  "warnings" : "...",           (string) any network and blockchain warnings.
    //}
    public class BlockchainInfoModel
    {
        [JsonProperty(PropertyName = "chain")]
        public string Chain { get; set; }

        [JsonProperty(PropertyName = "blocks")]
        public uint Blocks { get; set; }

        [JsonProperty(PropertyName = "headers")]
        public uint Headers { get; set; }

        [JsonProperty(PropertyName = "bestblockhash")]
        public uint256 BestBlockHash { get; set; }

        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty(PropertyName = "mediantime")]
        public long MedianTime { get; set; }

        [JsonProperty(PropertyName = "verificationprogress")]
        public double VerificationProgress { get; set; }

        [JsonProperty(PropertyName = "initialblockdownload")]
        public bool IsInitialBlockDownload { get; set; }

        [JsonProperty(PropertyName = "chainwork")]
        public uint256 Chainwork { get; set; }

        [JsonProperty(PropertyName = "pruned")]
        public bool IsPruned { get; set; }

        [JsonProperty(PropertyName = "softforks")]
        public List<SoftForks> SoftForks { get; set; }

        [JsonProperty(PropertyName = "bip9_softforks")]
        public Dictionary<string, SoftForksBip9> SoftForksBip9 { get; set; }
    }

    public class SoftForks
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }

        [JsonProperty(PropertyName = "reject")]
        public SoftForksStatus Status { get; set; }
    }

    public class SoftForksStatus
    {
        [JsonProperty(PropertyName = "status")]
        public bool Status { get; set; }
    }

    public class SoftForksBip9
    {
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        [JsonProperty(PropertyName = "startTime")]
        public int StartTime { get; set; }

        [JsonProperty(PropertyName = "timeout")]
        public int Timeout { get; set; }

        [JsonProperty(PropertyName = "since")]
        public int Since { get; set; }

        [JsonProperty(PropertyName = "bit")]
        public int Bit { get; set; }

        [JsonProperty(PropertyName = "statistics")]
        public SoftForksBip9Statistics Statistics { get; set; }

    }

    public class SoftForksBip9Statistics
    {
        [JsonProperty(PropertyName = "period")]
        public int Period { get; set; }

        [JsonProperty(PropertyName = "threshold")]
        public int Threshold { get; set; }

        [JsonProperty(PropertyName = "elapsed")]
        public int Elapsed { get; set; }

        [JsonProperty(PropertyName = "count")]
        public int Count { get; set; }

        [JsonProperty(PropertyName = "possible")]
        public bool Possible { get; set; }
    }
}

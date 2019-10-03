using System;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletGeneralInfoModel
    {
        /// <summary>
        /// The name of the Wallet
        /// </summary>
        [JsonProperty(PropertyName = "walletName")]
        public string WalletName { get; set; }

        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        [JsonProperty(PropertyName = "isDecrypted")]
        public bool IsDecrypted { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHeight")]
        public int? LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The total number of blocks.
        /// </summary>
        [JsonProperty(PropertyName = "chainTip")]
        public int? ChainTip { get; set; }

        /// <summary>
        /// Whether the chain is synced with the network.
        /// Only when this is true, can the client calculate a download percentage based on <see cref="ChainTip"/> and <see cref="LastBlockSyncedHeight"/>.
        /// </summary>
        [JsonProperty(PropertyName = "isChainSynced")]
        public bool IsChainSynced { get; set; }

        /// <summary>
        /// The total number of nodes that we're connected to.
        /// </summary>
        [JsonProperty(PropertyName = "connectedNodes")]
        public int ConnectedNodes { get; set; }
    }
}

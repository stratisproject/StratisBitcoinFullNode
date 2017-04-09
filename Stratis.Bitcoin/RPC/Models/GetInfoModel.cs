﻿using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.RPC.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.RPC.Models
{
#pragma warning disable IDE1006 // Naming Styles (ignore lowercase)
    public class GetInfoModel
    {
        [JsonProperty(Order = 0)]
        public uint version { get; set; }

        [JsonProperty(Order = 1)]
        public uint protocolversion { get; set; }

        [JsonProperty(Order = 4)]
        public int blocks { get; set; }

        [JsonProperty(Order = 5)]
        public double timeoffset { get; set; }

        [JsonProperty(Order = 6, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? connections { get; set; }

        [JsonProperty(Order = 7)]
        public string proxy { get; set; }

        [JsonProperty(Order = 8)]
        public double difficulty { get; set; }

        [JsonProperty(Order = 9)]
        public bool testnet { get; set; }

        [JsonConverter(typeof(BtcDecimalJsonConverter))]
        [JsonProperty(Order = 14)]
        public decimal relayfee { get; set; }

        [JsonProperty(Order = 15)]
        public string errors { get; set; }

        #region TODO: Wallet 

        [JsonProperty(Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object walletversion { get; set; }

        [JsonProperty(Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object balance { get; set; }

        [JsonProperty(Order = 10, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object keypoololdest { get; set; }

        [JsonProperty(Order = 11, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object keypoolsize { get; set; }

        [JsonProperty(Order = 12, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object unlocked_until { get; set; }

        [JsonProperty(Order = 13, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object paytxfee { get; set; }
        
        #endregion
    }
}
#pragma warning restore IDE1006 // Naming Styles

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;


namespace NBitcoin
{
    public class ValidatedAddress
    {
        [JsonProperty(PropertyName = "isvalid")]
        public bool IsValid { get; set; }
    }
}

using Newtonsoft.Json;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// A model that represents the result of calling the <see cref="ConnectionManagerController.AddNodeApiAsync(string, string)"/> API method.
    /// </summary>
    public sealed class AddNodeRpcResult
    {
        /// <summary> Reports any error that might have occurred during the call.</summary>
        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; }

        /// <summary> Indicates that the add node call succeeeded.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }
    }
}

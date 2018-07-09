using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;

namespace NBitcoin.OpenAsset
{
    public class CoinprismException : Exception
    {
        public CoinprismException()
        {
        }

        public CoinprismException(string message)
            : base(message)
        {
        }

        public CoinprismException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class CoinprismColoredTransactionRepository : IColoredTransactionRepository
    {
        private readonly Network network;

        private class CoinprismTransactionRepository : ITransactionRepository
        {
            #region ITransactionRepository Members

            public Task<Transaction> GetAsync(uint256 txId)
            {
                return Task.FromResult<Transaction>(null);
            }

            public Task PutAsync(uint256 txId, Transaction tx)
            {
                return Task.FromResult(true);
            }

            #endregion
        }

        public CoinprismColoredTransactionRepository(Network network)
        {
            this.network = network;
        }

        #region IColoredTransactionRepository Members

        public ITransactionRepository Transactions
        {
            get
            {
                return new CoinprismTransactionRepository();
            }
        }
       
        public async Task<ColoredTransaction> GetAsync(uint256 txId)
        {
            try
            {
                var result = new ColoredTransaction();

                string url = string.Empty;
                if (this.network.Name.ToLowerInvariant().Contains("test"))
                    url = string.Format("https://testnet.api.coinprism.com/v1/transactions/{0}", txId);
                else
                    url = string.Format("https://api.coinprism.com/v1/transactions/{0}", txId);

                HttpWebRequest req = WebRequest.CreateHttp(url);
                req.Method = "GET";

                using(WebResponse response = await req.GetResponseAsync().ConfigureAwait(false))
                {
                    var writer = new StreamReader(response.GetResponseStream());
                    string str = await writer.ReadToEndAsync().ConfigureAwait(false);
                    JObject json = JObject.Parse(str);
                    var inputs = json["inputs"] as JArray;
                    if(inputs != null)
                    {
                        for(int i = 0; i < inputs.Count; i++)
                        {
                            if(inputs[i]["asset_id"].Value<string>() == null)
                                continue;
                            var entry = new ColoredEntry();
                            entry.Index = (uint)i;
                            entry.Asset = new AssetMoney(
                                new BitcoinAssetId(inputs[i]["asset_id"].ToString(), null).AssetId,
                                inputs[i]["asset_quantity"].Value<ulong>());

                            result.Inputs.Add(entry);
                        }
                    }

                    var outputs = json["outputs"] as JArray;
                    if(outputs != null)
                    {
                        bool issuance = true;
                        for(int i = 0; i < outputs.Count; i++)
                        {
                            ColorMarker marker = ColorMarker.TryParse(new Script(Encoders.Hex.DecodeData(outputs[i]["script"].ToString())));
                            if(marker != null)
                            {
                                issuance = false;
                                result.Marker = marker;
                                continue;
                            }
                            if(outputs[i]["asset_id"].Value<string>() == null)
                                continue;
                            var entry = new ColoredEntry();
                            entry.Index = (uint)i;
                            entry.Asset = new AssetMoney(
                                new BitcoinAssetId(outputs[i]["asset_id"].ToString(), null).AssetId,
                                outputs[i]["asset_quantity"].Value<ulong>()
                                );

                            if(issuance)
                                result.Issuances.Add(entry);
                            else
                                result.Transfers.Add(entry);
                        }
                    }
                    return result;
                }
            }
            catch(WebException ex)
            {
                try
                {
                    JObject error = JObject.Parse(new StreamReader(ex.Response.GetResponseStream()).ReadToEnd());
                    if(error["ErrorCode"].ToString() == "InvalidTransactionHash")
                        return null;
                    throw new CoinprismException(error["ErrorCode"].ToString());
                }
                catch(CoinprismException)
                {
                    throw;
                }
                catch
                {
                }
                throw;
            }
        }

        public async Task BroadcastAsync(Transaction transaction)
        {
            if(transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            string url = string.Empty;
            if (this.network.Name.ToLowerInvariant().Contains("test"))
                url = "https://testnet.api.coinprism.com/v1/sendrawtransaction";
            else
                url = "https://api.coinprism.com/v1/transactions/v1/sendrawtransaction";

            HttpWebRequest req = WebRequest.CreateHttp(url);
            req.Method = "POST";
            req.ContentType = "application/json";

            Stream stream = await req.GetRequestStreamAsync().ConfigureAwait(false);
            var writer = new StreamWriter(stream);
            await writer.WriteAsync("\"" + transaction.ToHex() + "\"").ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            (await req.GetResponseAsync().ConfigureAwait(false)).Dispose();
        }

        public Task PutAsync(uint256 txId, ColoredTransaction tx)
        {
            return Task.FromResult(false);
        }

        #endregion
    }
}
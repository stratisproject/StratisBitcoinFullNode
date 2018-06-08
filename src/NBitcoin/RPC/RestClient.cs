﻿#if !NOJSONNET
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NBitcoin.Protocol.Payloads;
using Newtonsoft.Json.Linq;

namespace NBitcoin.RPC
{
    public enum RestResponseFormat
    {
        Bin,
        Hex,
        Json
    }

    /// <summary>
    /// Client class for the unauthenticated REST Interface
    /// </summary>
    public class RestClient : INBitcoinBlockRepository
    {
        private readonly Uri address;
        private readonly Network network;


        /// <summary>
        /// Gets the <see cref="Network"/> instance for the client.
        /// </summary>
        public Network Network
        {
            get
            {
                return network;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RestClient"/> class.
        /// </summary>
        /// <param name="address">The rest API endpoint</param>
        /// <exception cref="System.ArgumentNullException">Null rest API endpoint</exception>
        /// <exception cref="System.ArgumentException">Invalid value for RestResponseFormat</exception>
        public RestClient(Uri address)
            : this(address, Network.Main)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RestClient"/> class.
        /// </summary>
        /// <param name="address">The rest API endpoint</param>
        /// <param name="network">The network to operate with</param>
        /// <exception cref="System.ArgumentNullException">Null rest API endpoint</exception>
        /// <exception cref="System.ArgumentException">Invalid value for RestResponseFormat</exception>
        public RestClient(Uri address, Network network)
        {
            if (address == null)
                throw new ArgumentNullException("address");
            if (network == null)
                throw new ArgumentNullException("network");
            this.address = address;
            this.network = network;
        }

        /// <summary>
        /// Gets the block.
        /// </summary>
        /// <param name="blockId">The block identifier.</param>
        /// <returns>Given a block hash (id) returns the requested block object.</returns>
        /// <exception cref="System.ArgumentNullException">blockId cannot be null.</exception>
        public async Task<Block> GetBlockAsync(uint256 blockId)
        {
            if (blockId == null)
                throw new ArgumentNullException("blockId");

            byte[] result = await SendRequestAsync("block", RestResponseFormat.Bin, blockId.ToString()).ConfigureAwait(false);
            return new Block(result);
        }

        /// <summary>
        /// Gets the block.
        /// </summary>
        /// <param name="blockId">The block identifier.</param>
        /// <returns>Given a block hash (id) returns the requested block object.</returns>
        /// <exception cref="System.ArgumentNullException">blockId cannot be null.</exception>
        public Block GetBlock(uint256 blockId)
        {
            return GetBlockAsync(blockId).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets a transaction.
        /// </summary>
        /// <param name="txId">The transaction identifier.</param>
        /// <returns>Given a transaction hash (id) returns the requested transaction object.</returns>
        /// <exception cref="System.ArgumentNullException">txId cannot be null</exception>
        public async Task<Transaction> GetTransactionAsync(uint256 txId)
        {
            if (txId == null)
                throw new ArgumentNullException("txId");

            byte[] result = await SendRequestAsync("tx", RestResponseFormat.Bin, txId.ToString()).ConfigureAwait(false);
            return new Transaction(result);
        }

        /// <summary>
        /// Gets a transaction.
        /// </summary>
        /// <param name="txId">The transaction identifier.</param>
        /// <returns>Given a transaction hash (id) returns the requested transaction object.</returns>
        /// <exception cref="System.ArgumentNullException">txId cannot be null</exception>
        public Transaction GetTransaction(uint256 txId)
        {
            return GetTransactionAsync(txId).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets blocks headers.
        /// </summary>
        /// <param name="blockId">The initial block identifier.</param>
        /// <param name="count">how many headers to get.</param>
        /// <returns>Given a block hash (blockId) returns as much block headers as specified.</returns>
        /// <exception cref="System.ArgumentNullException">blockId cannot be null</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">count must be greater or equal to one.</exception>
        public async Task<IEnumerable<BlockHeader>> GetBlockHeadersAsync(uint256 blockId, int count)
        {
            if (blockId == null)
                throw new ArgumentNullException("blockId");

            if (count < 1)
                throw new ArgumentOutOfRangeException("count", "count must be greater or equal to one.");

            byte[] result = await SendRequestAsync("headers", RestResponseFormat.Bin, count.ToString(CultureInfo.InvariantCulture), blockId.ToString()).ConfigureAwait(false);

            const int hexSize = (BlockHeader.Size);

            return Enumerable
                .Range(0, result.Length / hexSize)
                .Select(i => BlockHeader.Load(result.SafeSubarray(i * hexSize, hexSize), this.network));
        }

        /// <summary>
        /// Gets blocks headers.
        /// </summary>
        /// <param name="blockId">The initial block identifier.</param>
        /// <param name="count">how many headers to get.</param>
        /// <returns>Given a block hash (blockId) returns as much block headers as specified.</returns>
        /// <exception cref="System.ArgumentNullException">blockId cannot be null</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">count must be greater or equal to one.</exception>
        public IEnumerable<BlockHeader> GetBlockHeaders(uint256 blockId, int count)
        {
            return GetBlockHeadersAsync(blockId, count).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the chain information.
        /// </summary>
        /// <returns></returns>
        public async Task<ChainInfo> GetChainInfoAsync()
        {
            byte[] result = await SendRequestAsync("chaininfo", RestResponseFormat.Json).ConfigureAwait(false);
            JObject o = JObject.Parse(Encoding.UTF8.GetString(result, 0, result.Length));

            return new ChainInfo
            {
                Chain = (string)o["chain"],
                BestBlockHash = uint256.Parse((string)o["bestblockhash"]),
                Blocks = (int)o["blocks"],
                ChainWork = uint256.Parse((string)o["chainwork"]),
                Difficulty = (int)o["difficulty"],
                Headers = (int)o["headers"],
                VerificationProgress = (decimal)o["verificationprogress"],
                IsPruned = (bool)o["pruned"]
            };
        }

        /// <summary>
        /// Gets unspect outputs.
        /// </summary>
        /// <param name="outPoints">The out points identifiers (TxIn-N).</param>
        /// <param name="checkMempool">if set to <c>true</c> [check mempool].</param>
        /// <returns>The unspent transaction outputs (UTXO) for the given outPoints.</returns>
        /// <exception cref="System.ArgumentNullException">outPoints cannot be null.</exception>
        public async Task<UTxOutputs> GetUnspentOutputsAsync(IEnumerable<OutPoint> outPoints, bool checkMempool)
        {
            if (outPoints == null)
                throw new ArgumentNullException("outPoints");

            IEnumerable<string> ids = from op in outPoints select op.ToString();

            byte[] result = await SendRequestAsync("getutxos" + (checkMempool ? "/checkmempool" : ""), RestResponseFormat.Bin, ids.ToArray()).ConfigureAwait(false);

            var mem = new MemoryStream(result);
            var utxos = new UTxOutputs();
            var stream = new BitcoinStream(mem, false);

            stream.ReadWrite(utxos);

            return utxos;
        }

        /// <summary>
        /// Gets an unspent transaction
        /// </summary>
        /// <param name="txid">The transaction id</param>
        /// <param name="vout">The vout of the transaction</param>
        /// <param name="includeMemPool">Whether or not to include the mempool</param>
        /// <returns>The unspent transaction for the specified transaction and vout</returns>
        public async Task<UnspentTransaction> GetTxOutAsync(uint256 txid, uint vout, bool includeMemPool = true)
        {
            byte[] result = await SendRequestAsync($"gettxout/{txid.ToString()}/{vout.ToString() + (includeMemPool ? "/includemempool" : "")}",
                            RestResponseFormat.Json).ConfigureAwait(false);

            string responseString = Encoding.UTF8.GetString(result, 0, result.Length);
            if (string.IsNullOrEmpty(responseString))
                return null;

            JObject objectResult = JObject.Parse(responseString);

            return new UnspentTransaction(objectResult);
        }

        public async Task<byte[]> SendRequestAsync(string resource, RestResponseFormat format, params string[] parms)
        {
            WebRequest request = BuildHttpRequest(resource, format, parms);

            using (WebResponse response = await GetWebResponseAsync(request).ConfigureAwait(false))
            {
                Stream stream = response.GetResponseStream();
                int bytesToRead = (int)response.ContentLength;
                byte[] buffer = await stream.ReadBytesAsync(bytesToRead).ConfigureAwait(false);

                return buffer;
            }
        }

#region Private methods
        private WebRequest BuildHttpRequest(string resource, RestResponseFormat format, params string[] parms)
        {
            bool hasParams = parms != null && parms.Length > 0;
            var uriBuilder = new UriBuilder(this.address);
            uriBuilder.Path = "rest/" + resource + (hasParams ? "/" : "") + string.Join("/", parms) + "." + format.ToString().ToLowerInvariant();

            HttpWebRequest request = WebRequest.CreateHttp(uriBuilder.Uri);
            request.Method = "GET";
#if !(PORTABLE || NETCORE)
            request.KeepAlive = false;
#endif
            return request;
        }

        private static async Task<WebResponse> GetWebResponseAsync(WebRequest request)
        {
            WebResponse response = null;
            WebException exception = null;

            try
            {
                response = await request.GetResponseAsync().ConfigureAwait(false);
            }
            catch(WebException ex)
            {
                // "WebException status: {0}", ex.Status);

                // Even if the request "failed" we need to continue reading the response from the router
                response = ex.Response as HttpWebResponse;

                if (response == null)
                    throw;

                exception = ex;
            }

            if (exception != null)
            {
                Stream stream = response.GetResponseStream();
                int bytesToRead = (int)response.ContentLength;
                byte[] buffer = await stream.ReadBytesAsync(bytesToRead).ConfigureAwait(false);
                response.Dispose();
                throw new RestApiException(Encoding.UTF8.GetString(buffer, 0, buffer.Length - 2), exception);
            }
            return response;
        }
#endregion
    }

    public class RestApiException : Exception
    {
        public RestApiException(string message, WebException inner)
            : base(message, inner)
        {
        }
    }

    public class ChainInfo
    {
        public string Chain { get; internal set; }
        public int Blocks { get; internal set; }
        public int Headers { get; internal set; }
        public uint256 BestBlockHash { get; internal set; }
        public int Difficulty { get; internal set; }
        public decimal VerificationProgress { get; internal set; }
        public uint256 ChainWork { get; internal set; }
        public bool IsPruned { get; internal set; }
    }
}
#endif
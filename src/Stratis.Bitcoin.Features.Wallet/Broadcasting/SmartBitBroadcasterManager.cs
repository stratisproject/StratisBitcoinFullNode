using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class SmartBitBroadcasterManager : BroadcasterManagerBase
    {
        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(3, 3);
        private HttpClient HttpClient;
        private string BaseUrl
        {
            get
            {
                return this.Network == Network.Main
                    ? "https://api.smartbit.com.au/v1/"
                    : "https://testnet-api.smartbit.com.au/v1/";
            }
        }

        public Network Network { get; }

        public SmartBitBroadcasterManager(Network network, HttpClientHandler handler = null) : base()
        {
            this.Network = network ?? throw new ArgumentNullException(nameof(network));
            if (network != Network.TestNet && network != Network.Main)
            {
                throw new ArgumentException($"{nameof(network)} can only be {Network.TestNet} or {Network.Main}");
            }

            if (handler == null)
            {
                this.HttpClient = new HttpClient();
            }
            else
            {
                this.HttpClient = new HttpClient(handler);
            }
        }

        /// <inheritdoc />
        public override async Task<Success> TryBroadcastAsync(Transaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            var found = GetTransaction(transaction.GetHash());
            if (found != null)
            {
                if (found.State == State.Propagated) return Success.Yes;
                if (found.State == State.CantBroadcast)
                {
                    AddOrUpdate(transaction, State.ToBroadcast);
                }
            }
            else
            {
                AddOrUpdate(transaction, State.ToBroadcast);
            }

            var post = $"{this.BaseUrl}blockchain/pushtx";
            var content = new StringContent(new JObject(new JProperty("hex", transaction.ToHex())).ToString(), Encoding.UTF8,
                "application/json");
            HttpResponseMessage smartBitResponse = null;
            await Semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                smartBitResponse = await this.HttpClient.PostAsync(post, content).ConfigureAwait(false);
            }
            finally
            {
                Semaphore.SafeRelease();
            }
            if (smartBitResponse == null)
            {
                throw new HttpRequestException($"{nameof(smartBitResponse)} is null");
            }
            if (smartBitResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new HttpRequestException($"SmartBit answered with {smartBitResponse.StatusCode}");
            }

            string response = await smartBitResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(response);

            if (json.Value<bool>("success"))
            {
                AddOrUpdate(transaction, State.Broadcasted);
                return Success.Yes;
            }
            else
            {
                AddOrUpdate(transaction, State.CantBroadcast);
                return Success.No;
            }
        }
    }
}
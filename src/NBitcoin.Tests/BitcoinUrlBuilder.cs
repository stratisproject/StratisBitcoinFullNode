using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
#if !NOHTTPCLIENT
using System.Net.Http;
using System.Net.Http.Headers;
#endif
using NBitcoin.Payment;

namespace NBitcoin.Tests
{
    /// <summary>
    /// https://github.com/bitcoin/bips/blob/master/bip-0021.mediawiki
    /// </summary>
    public class BitcoinUrlBuilder
    {
        public BitcoinUrlBuilder()
        {

        }
        public BitcoinUrlBuilder(Uri uri)
            : this(uri.AbsoluteUri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
        }

        public BitcoinUrlBuilder(string uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (!uri.StartsWith("bitcoin:", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Invalid scheme");

            uri = uri.Remove(0, "bitcoin:".Length);
            if (uri.StartsWith("//"))
                uri = uri.Remove(0, 2);

            var paramStart = uri.IndexOf('?');
            string address = null;

            if (paramStart == -1)
                address = uri;
            else
            {
                address = uri.Substring(0, paramStart);
                uri = uri.Remove(0, 1); // Remove?
            }

            if (address != string.Empty)
            {
                this.Address = Network.Parse<BitcoinAddress>(address, null);
            }

            uri = uri.Remove(0, address.Length);

            Dictionary<string, string> parameters;
            try
            {
                parameters = UriHelper.DecodeQueryParameters(uri);
            }
            catch (ArgumentException)
            {
                throw new FormatException("A URI parameter is duplicated");
            }

            if (parameters.ContainsKey("amount"))
            {
                this.Amount = Money.Parse(parameters["amount"]);
                parameters.Remove("amount");
            }

            if (parameters.ContainsKey("label"))
            {
                this.Label = parameters["label"];
                parameters.Remove("label");
            }

            if (parameters.ContainsKey("message"))
            {
                this.Message = parameters["message"];
                parameters.Remove("message");
            }

            if (parameters.ContainsKey("r"))
            {
                this.PaymentRequestUrl = new Uri(parameters["r"], UriKind.Absolute);
                parameters.Remove("r");
            }

            this.unknownParameters = parameters;

            var reqParam = parameters.Keys.FirstOrDefault(k => k.StartsWith("req-", StringComparison.OrdinalIgnoreCase));
            if (reqParam != null)
                throw new FormatException("Non compatible required parameter " + reqParam);
        }

        private readonly Dictionary<string, string> unknownParameters = new Dictionary<string, string>();
        public Dictionary<string, string> UnknowParameters
        {
            get
            {
                return this.unknownParameters;
            }
        }
#if !NOHTTPCLIENT
        public PaymentRequest GetPaymentRequest()
        {
            if (this.PaymentRequestUrl == null)
                throw new InvalidOperationException("No PaymentRequestUrl specified");

            return GetPaymentRequestAsync().GetAwaiter().GetResult();
        }
        public async Task<PaymentRequest> GetPaymentRequestAsync(HttpClient httpClient = null)
        {
            if (this.PaymentRequestUrl == null)
                throw new InvalidOperationException("No PaymentRequestUrl specified");

            bool own = false;
            if (httpClient == null)
            {
                httpClient = new HttpClient();
                own = true;
            }
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, this.PaymentRequestUrl);
                req.Headers.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(PaymentRequest.MediaType));

                HttpResponseMessage result = await httpClient.SendAsync(req).ConfigureAwait(false);

                if (!result.IsSuccessStatusCode)
                    throw new WebException(result.StatusCode + "(" + (int)result.StatusCode + ")");

                if (result.Content.Headers.ContentType == null || !result.Content.Headers.ContentType.MediaType.Equals(PaymentRequest.MediaType, StringComparison.OrdinalIgnoreCase))
                {
                    throw new WebException("Invalid contenttype received, expecting " + PaymentRequest.MediaType + ", but got " + result.Content.Headers.ContentType);
                }

                Stream stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return PaymentRequest.Load(stream);
            }
            finally
            {
                if (own)
                    httpClient.Dispose();
            }
        }
#endif
        /// <summary>
        /// https://github.com/bitcoin/bips/blob/master/bip-0072.mediawiki
        /// </summary>
        public Uri PaymentRequestUrl { get; set; }
        public BitcoinAddress Address { get; set; }    
        public Money Amount { get; set; }
        public string Label { get; set; }
        public string Message { get; set; }

        public Uri Uri
        {
            get
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                StringBuilder builder = new StringBuilder();
                builder.Append("bitcoin:");
                if (this.Address != null)
                {
                    builder.Append(this.Address.ToString());
                }

                if (this.Amount != null)
                {
                    parameters.Add("amount", this.Amount.ToString(false, true));
                }

                if (this.Label != null)
                {
                    parameters.Add("label", this.Label.ToString());
                }

                if (this.Message != null)
                {
                    parameters.Add("message", this.Message.ToString());
                }

                if (this.PaymentRequestUrl != null)
                {
                    parameters.Add("r", this.PaymentRequestUrl.ToString());
                }

                foreach (KeyValuePair<string, string> kv in this.UnknowParameters)
                {
                    parameters.Add(kv.Key, kv.Value);
                }

                WriteParameters(parameters, builder);

                return new System.Uri(builder.ToString(), UriKind.Absolute);
            }
        }

        private static void WriteParameters(Dictionary<string, string> parameters, StringBuilder builder)
        {
            bool first = true;
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                if (first)
                {
                    first = false;
                    builder.Append("?");
                }
                else
                    builder.Append("&");

                builder.Append(parameter.Key);
                builder.Append("=");
                builder.Append(WebUtility.UrlEncode(parameter.Value));
            }
        }

        public override string ToString()
        {
            return this.Uri.AbsoluteUri;
        }
    }
}

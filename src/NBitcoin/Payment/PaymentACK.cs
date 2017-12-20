using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using NBitcoin.Protobuf;
#if !NOHTTPCLIENT
using System.Net.Http;
using System.Net.Http.Headers;
#endif

namespace NBitcoin.Payment
{
    public class PaymentACK
    {
        public const int MaxLength = 60000;
        public readonly static string MediaType = "application/bitcoin-paymentack";

        public PaymentMessage Payment { get; set; }
        public string Memo { get; set; }

        public static PaymentACK Load(byte[] data)
        {
            return Load(new MemoryStream(data));
        }

        public static PaymentACK Load(Stream source)
        {
            if (source.CanSeek && source.Length > MaxLength)
                throw new ArgumentOutOfRangeException("PaymentACK messages larger than " + MaxLength + " bytes should be rejected", "source");

            PaymentACK ack = new PaymentACK();
            Protobuf.ProtobufReaderWriter reader = new Protobuf.ProtobufReaderWriter(source);
            int key;
            while(reader.TryReadKey(out key))
            {
                switch(key)
                {
                    case 1:
                        byte[] bytes = reader.ReadBytes();
                        ack.Payment = PaymentMessage.Load(bytes);
                        break;
                    case 2:
                        ack.Memo = reader.ReadString();
                        break;
                    default:
                        break;
                }
            }

            return ack;
        }

        public PaymentACK()
        {
        }

        public PaymentACK(PaymentMessage payment)
        {
            this.Payment = payment;
        }

        public byte[] ToBytes()
        {
            MemoryStream ms = new MemoryStream();
            WriteTo(ms);
            return ms.ToArray();
        }

        public void WriteTo(Stream output)
        {
            Protobuf.ProtobufReaderWriter proto = new ProtobufReaderWriter(output);
            proto.WriteKey(1, ProtobufReaderWriter.PROTOBUF_LENDELIM);
            proto.WriteBytes(this.Payment.ToBytes());
            if (this.Memo != null)
            {
                proto.WriteKey(2, ProtobufReaderWriter.PROTOBUF_LENDELIM);
                proto.WriteString(this.Memo);
            }
        }
#if !NOFILEIO
        public static PaymentACK Load(string file)
        {
            using (FileStream fs = File.OpenRead(file))
            {
                return Load(fs);
            }
        }
#endif
    }

    public class PaymentMessage
    {
        public const int MaxLength = 50000;
        public readonly static string MediaType = "application/bitcoin-payment";
        public string Memo { get; set; }
        public byte[] MerchantData { get; set; }
        public Uri ImplicitPaymentUrl { get; set; }
        public List<Transaction> Transactions { get; } = new List<Transaction>();
        public List<PaymentOutput> RefundTo { get; } = new List<PaymentOutput>();

        public static PaymentMessage Load(byte[] data)
        {
            return Load(new MemoryStream(data));
        }

        public PaymentACK CreateACK(string memo = null)
        {
            return new PaymentACK(this)
            {
                Memo = memo
            };
        }

        public PaymentMessage()
        {
        }

        public PaymentMessage(PaymentRequest request)
        {
            this.MerchantData = request.Details.MerchantData;
        }

        public static PaymentMessage Load(Stream source)
        {
            if (source.CanSeek && source.Length > MaxLength)
                throw new ArgumentException("Payment messages larger than " + MaxLength + " bytes should be rejected by the merchant's server", "source");

            PaymentMessage message = new PaymentMessage();
            ProtobufReaderWriter proto = new ProtobufReaderWriter(source);

            int key;
            while(proto.TryReadKey(out key))
            {
                switch(key)
                {
                    case 1:
                        message.MerchantData = proto.ReadBytes();
                        break;
                    case 2:
                        message.Transactions.Add(new Transaction(proto.ReadBytes()));
                        break;
                    case 3:
                        message.RefundTo.Add(PaymentOutput.Load(proto.ReadBytes()));
                        break;
                    case 4:
                        message.Memo = proto.ReadString();
                        break;
                    default:
                        break;
                }
            }

            return message;
        }

        public byte[] ToBytes()
        {
            MemoryStream ms = new MemoryStream();
            WriteTo(ms);
            return ms.ToArray();
        }

        public void WriteTo(Stream output)
        {
            var proto = new ProtobufReaderWriter(output);
            if (this.MerchantData != null)
            {
                proto.WriteKey(1, ProtobufReaderWriter.PROTOBUF_LENDELIM);
                proto.WriteBytes(this.MerchantData);
            }

            foreach (Transaction tx in this.Transactions)
            {
                proto.WriteKey(2, ProtobufReaderWriter.PROTOBUF_LENDELIM);
                proto.WriteBytes(tx.ToBytes());
            }

            foreach (PaymentOutput txout in this.RefundTo)
            {
                proto.WriteKey(3, ProtobufReaderWriter.PROTOBUF_LENDELIM);
                proto.WriteBytes(txout.ToBytes());
            }

            if (this.Memo != null)
            {
                proto.WriteKey(4, ProtobufReaderWriter.PROTOBUF_LENDELIM);
                proto.WriteString(this.Memo);
            }
        }

#if !NOHTTPCLIENT
        /// <summary>
        /// Send the payment to given address
        /// </summary>
        /// <param name="paymentUrl">ImplicitPaymentUrl if null</param>
        /// <returns>The PaymentACK</returns>
        public PaymentACK SubmitPayment(Uri paymentUrl = null)
        {
            if (paymentUrl == null)
                paymentUrl = this.ImplicitPaymentUrl ?? throw new ArgumentNullException("paymentUrl");

            try
            {
                return SubmitPaymentAsync(paymentUrl, null).Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        public async Task<PaymentACK> SubmitPaymentAsync(Uri paymentUrl, HttpClient httpClient)
        {
            bool own = false;

            if (paymentUrl == null)
                paymentUrl = this.ImplicitPaymentUrl ?? throw new ArgumentNullException("paymentUrl");

            if (httpClient == null)
            {
                httpClient = new HttpClient();
                own = true;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, paymentUrl.OriginalString);
                request.Headers.Clear();
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(PaymentACK.MediaType));
                request.Content = new ByteArrayContent(this.ToBytes());
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(PaymentMessage.MediaType);

                HttpResponseMessage result = await httpClient.SendAsync(request).ConfigureAwait(false);
                if (!result.IsSuccessStatusCode)
                    throw new WebException(result.StatusCode + "(" + (int)result.StatusCode + ")");

                if (result.Content.Headers.ContentType == null || !result.Content.Headers.ContentType.MediaType.Equals(PaymentACK.MediaType, StringComparison.OrdinalIgnoreCase))
                    throw new WebException("Invalid contenttype received, expecting " + PaymentACK.MediaType + ", but got " + result.Content.Headers.ContentType);

                Stream response = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return PaymentACK.Load(response);
            }
            finally
            {
                if (own)
                    httpClient.Dispose();
            }
        }
#endif
#if !NOFILEIO
        public static PaymentMessage Load(string file)
        {
            using (FileStream fs = File.OpenRead(file))
            {
                return Load(fs);
            }
        }
#endif
    }
}
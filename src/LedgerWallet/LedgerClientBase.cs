using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LedgerWallet.Transports;

namespace LedgerWallet
{
    public class APDUResponse
    {
        public byte[] Response
        {
            get; set;
        }

        public int SW
        {
            get; set;
        }
    }

    public abstract class LedgerClientBase
    {
        public int MaxAPDUSize { get; set; } = 260;

        public static int[] OK = new[] { LedgerWalletConstants.SW_OK };

        public ILedgerTransport Transport { get; }

        public LedgerClientBase(ILedgerTransport transport)
        {
            Transport = transport ?? throw new ArgumentNullException("transport");
        }

        protected byte[] CreateAPDU(byte cla, byte ins, byte p1, byte p2, byte[] data)
        {
            var apdu = new byte[data.Length + 5];
            apdu[0] = cla;
            apdu[1] = ins;
            apdu[2] = p1;
            apdu[3] = p2;
            apdu[4] = (byte)(data.Length);
            Array.Copy(data, 0, apdu, 5, data.Length);
            return apdu;
        }

        protected byte[][] CreateAPDUSplit(byte cla, byte ins, byte p1, byte p2, byte[] data)
        {
            var offset = 0;
            var result = new List<byte[]>();
            while(offset < data.Length)
            {
                var blockLength = Math.Min(data.Length - offset, MaxAPDUSize - 5);
                var apdu = new byte[blockLength + 5];
                apdu[0] = cla;
                apdu[1] = ins;
                apdu[2] = p1;
                apdu[3] = p2;
                apdu[4] = (byte)(blockLength);
                Array.Copy(data, offset, apdu, 5, blockLength);
                result.Add(apdu);
                offset += blockLength;
            }
            return result.ToArray();
        }

        protected byte[][] CreateApduSplit2(byte cla, byte ins, byte p1, byte p2, byte[] data, byte[] data2)
        {
            var offset = 0;
            var maxBlockSize = MaxAPDUSize - 5 - data2.Length;
            var apdus = new List<byte[]>();
            while(offset < data.Length)
            {
                var blockLength = ((data.Length - offset) > maxBlockSize ? maxBlockSize : data.Length - offset);
                var lastBlock = ((offset + blockLength) == data.Length);
                var apdu = new byte[blockLength + 5 + (lastBlock ? data2.Length : 0)];
                apdu[0] = cla;
                apdu[1] = ins;
                apdu[2] = p1;
                apdu[3] = p2;
                apdu[4] = (byte)(blockLength + (lastBlock ? data2.Length : 0));
                Array.Copy(data, offset, apdu, 5, blockLength);
                if(lastBlock)
                {
                    Array.Copy(data2, 0, apdu, 5 + blockLength, data2.Length);
                }
                apdus.Add(apdu);
                offset += blockLength;
            }
            return apdus.ToArray();
        }

        protected void Throw(int sw)
        {
            var status = new LedgerWalletStatus(sw);
            throw new LedgerWalletException(GetErrorMessage(status), status);
        }

        protected void CheckSW(int[] acceptedSW, int sw)
        {
            if(!acceptedSW.Contains(sw))
            {
                Throw(sw);
            }
        }

        protected virtual string GetErrorMessage(LedgerWalletStatus status)
        {
            switch(status.SW)
            {
                case 0x6D00:
                    return "INS not supported";
                case 0x6E00:
                    return "CLA not supported";
                case 0x6700:
                    return "Incorrect length";
                case 0x6982:
                    return "Command not allowed : Security status not satisfied";
                case 0x6985:
                    return "Command not allowed : Conditions of use not satisfied";
                case 0x6A80:
                    return "Invalid data";
                case 0x6482:
                    return "File not found";
                case 0x6B00:
                    return "Incorrect parameter P1 or P2";
                case 0x9000:
                    return "OK";
                default:
                    {
                        if((status.SW & 0xFF00) != 0x6F00)
                            return "Unknown error";
                        switch(status.InternalSW)
                        {
                            case 0xAA:
                                return "The dongle must be reinserted";
                            default:
                                return "Unknown error";
                        }
                    }
            }
        }

        protected Task<byte[]> ExchangeSingleAPDUAsync(byte cla, byte ins, byte p1, byte p2, byte[] data, int[] acceptedSW, CancellationToken cancellation)
        {
            return ExchangeApdusAsync(new byte[][] { CreateAPDU(cla, ins, p1, p2, data) }, acceptedSW, cancellation);
        }

        protected Task<APDUResponse> ExchangeSingleAPDUAsync(byte cla, byte ins, byte p1, byte p2, byte[] data, CancellationToken cancellation)
        {
            return ExchangeSingleAsync(new byte[][] { CreateAPDU(cla, ins, p1, p2, data) }, cancellation);
        }

        protected Task<byte[]> ExchangeSingleAPDUAsync(byte cla, byte ins, byte p1, byte p2, int length, int[] acceptedSW, CancellationToken cancellation)
        {
            var apdu = new byte[]
            {
                cla,ins,p1,p2,(byte)length
            };
            return ExchangeApdusAsync(new byte[][] { apdu }, acceptedSW, cancellation);
        }

        protected async Task<byte[]> ExchangeApdusAsync(byte[][] apdus, int[] acceptedSW, CancellationToken cancellation)
        {
            var resp = await ExchangeSingleAsync(apdus, cancellation).ConfigureAwait(false);
            CheckSW(acceptedSW, resp.SW);
            return resp.Response;
        }

        protected async Task<APDUResponse> ExchangeSingleAsync(byte[][] apdus, CancellationToken cancellation)
        {
            var responses = await ExchangeAsync(apdus, cancellation).ConfigureAwait(false);
            var last = responses.Last();
            foreach(var response in responses)
            {
                if(response != last)
                    CheckSW(OK, response.SW);
            }
            return last;
        }

        protected async Task<APDUResponse[]> ExchangeAsync(byte[][] apdus, CancellationToken cancellation)
        {
            var responses = await Transport.Exchange(apdus, cancellation).ConfigureAwait(false);
            var resultResponses = new List<APDUResponse>();
            foreach(var response in responses)
            {
                if(response.Length < 2)
                {
                    throw new LedgerWalletException("Truncated response");
                }
                var sw = ((response[response.Length - 2] & 0xff) << 8) |
                        response[response.Length - 1] & 0xff;
                if(sw == 0x6faa)
                    Throw(sw);
                var result = new byte[response.Length - 2];
                Array.Copy(response, 0, result, 0, response.Length - 2);
                resultResponses.Add(new APDUResponse() { Response = result, SW = sw });
            }
            return resultResponses.ToArray();
        }
    }
}

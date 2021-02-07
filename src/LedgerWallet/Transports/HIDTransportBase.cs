using LedgerWallet.HIDProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{

    public abstract class HIDTransportBase : ILedgerTransport
    {
        internal IHIDDevice _Device;
        readonly VendorProductIds _VendorProductIds;

        protected HIDTransportBase(IHIDDevice device, UsageSpecification[] acceptedUsageSpecifications)
        {
            _Device = device ?? throw new ArgumentNullException(nameof(device));

            _VendorProductIds = new VendorProductIds(device.VendorId, device.ProductId);
            _AcceptedUsageSpecifications = acceptedUsageSpecifications;
        }

        UsageSpecification[] _AcceptedUsageSpecifications;

        bool needInit = true;
        public string DevicePath
        {
            get
            {
                return _Device.DevicePath;
            }
        }

        protected SemaphoreSlim _SemaphoreSlim = new SemaphoreSlim(1, 1);
        bool initializing = false;
        public async Task<byte[][]> Exchange(byte[][] apdus, CancellationToken cancellation)
        {
            if(needInit && !initializing)
            {
                initializing = true;
                await InitAsync(cancellation);
                needInit = false;
                initializing = false;
            }
            var response = await ExchangeCoreAsync(apdus, cancellation).ConfigureAwait(false);

            if(response == null)
            {
                if(!await RenewTransportAsync(cancellation))
                {
                    throw new LedgerWalletException("Ledger disconnected");
                }
                response = await ExchangeCoreAsync(apdus, cancellation).ConfigureAwait(false);
                if(response == null)
                    throw new LedgerWalletException("Error while transmission");
            }

            return response;
        }

        async Task<bool> RenewTransportAsync(CancellationToken cancellation)
        {
            _Device = _Device.Clone();
            try
            {
                await _Device.EnsureInitializedAsync(cancellation);
            }
            catch(HIDDeviceException) { return false; }
            return await _Device.IsConnectedAsync();
        }

        protected virtual Task InitAsync(CancellationToken cancellation)
        {
#if(NETSTANDARD2_0)
            return Task.CompletedTask;
#else
            return Task.FromResult<bool>(true);
#endif
        }


        const uint MAX_BLOCK = 64;
        const int DEFAULT_TIMEOUT = 20000;

        internal async Task<byte[][]> ExchangeCoreAsync(byte[][] apdus, CancellationToken cancellation)
        {
            if(apdus == null || apdus.Length == 0)
                return null;
            var resultList = new List<byte[]>();
            var lastAPDU = apdus.Last();

            await _SemaphoreSlim.WaitAsync();

            try
            {
                foreach(var apdu in apdus)
                {
                    await WriteAsync(apdu, cancellation);
                    var result = await ReadAsync(cancellation);
                    if(result == null)
                        return null;
                    resultList.Add(result);
                }
            }
            finally
            {
                _SemaphoreSlim.Release();
            }

            return resultList.ToArray();
        }

        protected async Task<byte[]> ReadAsync(CancellationToken cancellation)
        {
            var response = new MemoryStream();
            var remaining = 0;
            var sequenceIdx = 0;
            try
            {

                do
                {
                    var result = await _Device.ReadAsync(cancellation);
                    var commandPart = UnwrapReponseAPDU(result, ref sequenceIdx, ref remaining);
                    if(commandPart == null)
                        return null;
                    response.Write(commandPart, 0, commandPart.Length);
                } while(remaining != 0);

            }
            catch(HIDDeviceException)
            {
                return null;
            }
            return response.ToArray();
        }

        protected async Task<byte[]> WriteAsync(byte[] apdu, CancellationToken cancellation)
        {
            var sequenceIdx = 0;
            byte[] packet = null;
            var apduStream = new MemoryStream(apdu);
            do
            {
                packet = WrapCommandAPDU(apduStream, ref sequenceIdx);
                await _Device.WriteAsync(packet, 0, packet.Length, cancellation);
            } while(apduStream.Position != apduStream.Length);
            return packet;
        }

        protected abstract byte[] UnwrapReponseAPDU(byte[] packet, ref int sequenceIdx, ref int remaining);

        protected abstract byte[] WrapCommandAPDU(Stream apduStream, ref int sequenceIdx);
    }
}

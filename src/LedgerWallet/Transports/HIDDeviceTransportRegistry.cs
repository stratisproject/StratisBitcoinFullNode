using LedgerWallet.HIDProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
    public class HIDDeviceTransportRegistry<T> where T : HIDTransportBase
    {
        readonly Func<IHIDDevice, T> create;
        public HIDDeviceTransportRegistry(Func<IHIDDevice, T> create)
        {
            this.create = create;
        }


        public async Task<IEnumerable<T>> GetHIDTransportsAsync(IEnumerable<VendorProductIds> ids, UsageSpecification[] acceptedUsages, CancellationToken cancellation)
        {
            var provider = HIDProvider.Provider;
            if(provider == null)
                throw new InvalidOperationException("You must set the static member HIDProvider.Provider");
            var devices = (await provider.EnumerateDeviceDescriptions(ids, acceptedUsages))
                            .Select(d => GetTransportAsync(provider, d, cancellation))
                            .ToList();
            await Task.WhenAll(devices);
            return devices.Select(d => d.GetAwaiter().GetResult());
        }


        readonly Dictionary<string, T> _TransportsByDevicePath = new Dictionary<string, T>();
        protected SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);

        private async Task<T> GetTransportAsync(IHIDProvider provider, HIDDeviceInformation device, CancellationToken cancellation)
        {
            await _Lock.WaitAsync();

            try
            {
                T transport = null;
                var uniqueId = string.Format("[{0},{1}]{2}", device.VendorId, device.ProductId, device.DevicePath);
                if(_TransportsByDevicePath.TryGetValue(uniqueId, out transport))
                    return transport;
                var hidDevice = provider.CreateFromDescription(device);
                await hidDevice.EnsureInitializedAsync(cancellation);
                transport = create(hidDevice);
                _TransportsByDevicePath.Add(uniqueId, transport);
                return transport;
            }
            finally
            {
                _Lock.Release();
            }
        }
    }
}

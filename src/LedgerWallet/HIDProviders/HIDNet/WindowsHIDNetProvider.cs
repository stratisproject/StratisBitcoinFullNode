using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hid.Net;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public class WindowsHIDNetProvider : IHIDProvider
    {
        public IHIDDevice CreateFromDescription(HIDDeviceInformation decription)
        {
            return new WindowsHIDNetDevice((DeviceInformation)decription.ProviderInformation);
        }


        public Task<IEnumerable<HIDDeviceInformation>> EnumerateDeviceDescriptions(IEnumerable<VendorProductIds> vendorProductIds, UsageSpecification[] acceptedUsages)
        {
            var devices = new List<DeviceInformation>();

            var collection = WindowsHidDevice.GetConnectedDeviceInformations();

            foreach(var ids in vendorProductIds)
            {
                if(ids.ProductId == null)
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId));
                else
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId && c.ProductId == ids.ProductId));

            }
            var retVal = devices
                .Where(d =>
                acceptedUsages == null ||
                acceptedUsages.Length == 0 ||
                acceptedUsages.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage)).ToList();

            return Task.FromResult<IEnumerable<HIDDeviceInformation>>(retVal.Select(r => new HIDDeviceInformation()
            {
                ProductId = r.ProductId,
                VendorId = r.VendorId,
                DevicePath = r.DevicePath,
                ProviderInformation = r
            }));
        }
    }
}

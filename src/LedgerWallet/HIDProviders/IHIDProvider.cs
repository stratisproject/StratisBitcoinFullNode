using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.HIDProviders
{
    public interface IHIDProvider
    {
        Task<IEnumerable<HIDDeviceInformation>> EnumerateDeviceDescriptions(IEnumerable<VendorProductIds> vendorProductIds, UsageSpecification[] acceptedUsages);
        IHIDDevice CreateFromDescription(HIDDeviceInformation decription);
    }
    public class HIDDeviceInformation
    {
        public ushort VendorId
        {
            get; set;
        }
        public ushort ProductId
        {
            get; set;
        }
        public string DevicePath
        {
            get; set;
        }
        public object ProviderInformation
        {
            get; set;
        }
    }
    public interface IHIDDevice
    {

        int VendorId
        {
            get;
        }
        int ProductId
        {
            get;
        }

        string DevicePath
        {
            get;
        }

        Task<bool> IsConnectedAsync();

        Task EnsureInitializedAsync(CancellationToken cancellation);

        Task<byte[]> ReadAsync(CancellationToken cancellation);
        Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellation);

        IHIDDevice Clone();
    }
}

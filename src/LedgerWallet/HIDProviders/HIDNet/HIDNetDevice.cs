using System;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public abstract class HIDNetDevice : IHIDDevice
    {
        internal readonly Hid.Net.IHidDevice _Device;
        internal readonly Hid.Net.DeviceInformation _DeviceInformation;
        public HIDNetDevice(Hid.Net.DeviceInformation deviceInformation, Hid.Net.IHidDevice hid)
        {
            _Device = hid ?? throw new ArgumentNullException(nameof(hid));
            _DeviceInformation = deviceInformation ?? throw new ArgumentNullException(nameof(deviceInformation));
        }

        public string DevicePath => _DeviceInformation.DevicePath;

        public int VendorId => _Device.VendorId;

        public int ProductId => _Device.ProductId;

        public abstract IHIDDevice Clone();

        public abstract Task EnsureInitializedAsync(CancellationToken cancellation);

        public Task<bool> IsConnectedAsync()
        {
            return _Device.GetIsConnectedAsync();
        }

        public async Task<byte[]> ReadAsync(CancellationToken cancellation)
        {
            //Note: this method used to read 64 bytes and shift that right in to an array of 65 bytes
            //Android does this automatically, so, we can't do this here for compatibility with Android.
            //See the flag DataHasExtraByte on WindowsHidDevice and UWPHidDevice

            try
            {
                return await _Device.ReadAsync().WithCancellation(cancellation);
            }
            catch(System.IO.IOException ex)
            {
                throw new HIDDeviceException(ex.Message, ex);
            }
        }

        public async Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellation)
        {
            //Note: this method used to take 64 bytes and shift that right in to an array of 65 bytes and then write to the device
            //Android does this automatically, so, we can't do this here for compatibility with Android.
            //See the flag DataHasExtraByte on WindowsHidDevice and UWPHidDevice

            if(offset != 0 || length != buffer.Length)
            {
                var newBuffer = new byte[buffer.Length - offset];
                Buffer.BlockCopy(buffer, offset, newBuffer, 0, length);
                buffer = newBuffer;
            }
            try
            {
                await _Device.WriteAsync(buffer).WithCancellation(cancellation);
            }
            catch(System.IO.IOException ex)
            {
                throw new HIDDeviceException(ex.Message, ex);
            }
        }
    }
}

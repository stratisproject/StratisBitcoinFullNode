using System;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public class WindowsHIDNetDevice : HIDNetDevice
    {
        readonly Hid.Net.WindowsHidDevice _Windows;
        public WindowsHIDNetDevice(Hid.Net.DeviceInformation deviceInfo) : base(deviceInfo, new Hid.Net.WindowsHidDevice(deviceInfo)
        {
            DataHasExtraByte = true
        })
        {
            _Windows = (Hid.Net.WindowsHidDevice)base._Device;
        }
        public override IHIDDevice Clone()
        {
            return new WindowsHIDNetDevice(_DeviceInformation);
        }

        public override async Task EnsureInitializedAsync(CancellationToken cancellation)
        {
            if(!_Windows.IsInitialized)
            {
                try
                {
                    await _Windows.InitializeAsync();
                }
                catch(Exception ex)
                {
                    throw new HIDDeviceException(ex.Message, ex);
                }
            }
        }
    }
}

using LedgerWallet.HIDProviders.HIDNet;
using System.Runtime.InteropServices;

namespace LedgerWallet.HIDProviders
{
    public class HIDProvider
    {
        static HIDProvider()
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // Must be different for UWP
            {
                Provider = new WindowsHIDNetProvider();
            }
        }
        public static IHIDProvider Provider
        {
            get; set;
        }
    }
}

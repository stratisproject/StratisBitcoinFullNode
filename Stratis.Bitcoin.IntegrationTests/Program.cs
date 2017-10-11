using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.IntegrationTests
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            await (new WalletTests()).CanSendToAddressAsync().ConfigureAwait(false);
        }
    }
}

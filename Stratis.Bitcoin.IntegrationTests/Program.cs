using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.IntegrationTests
{
    class Program
    {
        public static void Main(string[] args)
        {
            new WalletTests().CanMineBlocks();
        }
    }
}

namespace Stratis.Bitcoin.IntegrationTests
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            new WalletTests().CanMineAndSendToAddress();
        }
    }
}

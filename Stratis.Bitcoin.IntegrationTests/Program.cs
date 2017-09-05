namespace Stratis.Bitcoin.IntegrationTests
{
    class Program
    {
        public static void Main(string[] args)
        {
            new WalletTests().CanSendToAddress();
        }
    }
}

using System.Linq;
using System.Text;

namespace LedgerWallet
{
    public class GetCoinVersionResult
    {
        #region Constants
        private const int CoinLengthPos = 5;
        private const int SpacerLength = 2;
        #endregion

        #region Public Properties
        public string CoinName { get; private set; }
        public string ShortCoinName { get; private set; }
        #endregion

        #region Constructor
        public GetCoinVersionResult(byte[] bytes)
        {
            var coinLength = bytes[CoinLengthPos];
            var shortCoinNameStartPos = (CoinLengthPos + SpacerLength) + coinLength;
            var shortCoinLength = bytes[shortCoinNameStartPos - 1];

            var responseList = bytes.ToList();

            var coinNameData = responseList.GetRange(6, coinLength).ToArray();
            var shortCoinNameData = responseList.GetRange(shortCoinNameStartPos, shortCoinLength).ToArray();

            CoinName = Encoding.ASCII.GetString(coinNameData);
            ShortCoinName = Encoding.ASCII.GetString(shortCoinNameData);
        }
        #endregion
    }
}
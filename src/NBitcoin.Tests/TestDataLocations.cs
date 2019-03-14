using System.IO;

namespace Stratis.Bitcoin.NBitcoin.Tests
{
    public class TestDataLocations
    {
        public static string GetFileFromDataFolder(string filename)
        {
            return Path.Combine("data", filename);
        }

        public static string GetFileFromDataBlockFolder(string filename)
        {
            return Path.Combine("data", "blocks", filename);
        }

        public static string GetFileFromDataPosFolder(string filename)
        {
            return Path.Combine("data_pos", filename);
        }

        public static string GetFileFromDataPosBlockFolder(string filename)
        {
            return Path.Combine("data_pos", "blocks", filename);
        }
    }
}

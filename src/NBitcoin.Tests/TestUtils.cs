using System.IO;
using NBitcoin.DataEncoders;

namespace NBitcoin.Tests
{
    internal class TestUtils
    {
        public static byte[] ToBytes(string str)
        {
            var result = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
            {
                result[i] = (byte)str[i];
            }
            return result;
        }

        internal static byte[] ParseHex(string data)
        {
            return Encoders.Hex.DecodeData(data);
        }

        public static Block CreateFakeBlock(Network network, Transaction tx)
        {
            Block block = network.CreateBlock();
            block.AddTransaction(tx);
            block.UpdateMerkleRoot();
            return block;
        }

        public static Block CreateFakeBlock(Network network)
        {
            Block block = CreateFakeBlock(network, network.CreateTransaction());
            block.Header.HashPrevBlock = new uint256(RandomUtils.GetBytes(32));
            block.Header.Nonce = RandomUtils.GetUInt32();
            return block;
        }

        internal static void EnsureNew(string folderName)
        {
            if (Directory.Exists(folderName))
                Directory.Delete(folderName, true);

            while (true)
            {
                try
                {
                    Directory.CreateDirectory(folderName);
                    break;
                }
                catch
                {
                }
            }
        }
    }
}
using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class KeyToolTests
    {
        private readonly KeyTool tool;

        private readonly DataFolder dataFolder;

        public KeyToolTests()
        {
            string testRootPath = Path.Combine(Path.GetTempPath(), this.GetType().Name + "_" + nameof(this.CanSaveLoadKey));
            Directory.CreateDirectory(testRootPath);

            this.dataFolder = new DataFolder(testRootPath);
            this.tool = new KeyTool(this.dataFolder);
        }

        [Fact]
        public void CanGenerateKey()
        {
            Key key1 = this.tool.GeneratePrivateKey();
            Key key2 = this.tool.GeneratePrivateKey();

            Assert.NotEqual(key1.PubKey, key2.PubKey);

            int bytesCount = key1.PubKey.ToBytes().Length;
            Assert.Equal(33, bytesCount);
        }

        [Fact]
        public void CanSaveLoadKey()
        {
            Key key = this.tool.GeneratePrivateKey();

            this.tool.SavePrivateKey(key);
            Key loadedKey = this.tool.LoadPrivateKey();

            Assert.Equal(loadedKey.PubKey, key.PubKey);

            Directory.Delete(this.dataFolder.RootPath, true);
        }
    }
}

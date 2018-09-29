using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class KeyToolTests
    {
        private readonly KeyTool tool;

        public KeyToolTests()
        {
            this.tool = new KeyTool();
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
            string testPath = Path.Combine(Path.GetTempPath(), this.GetType().Name + "_" + nameof(this.CanSaveLoadKey));
            Directory.CreateDirectory(testPath);

            string filePath = testPath + @"\key.dat";

            Key key = this.tool.GeneratePrivateKey();

            this.tool.SavePrivateKey(key, filePath);
            Key loadedKey = this.tool.LoadPrivateKey(filePath);

            Assert.Equal(loadedKey.PubKey, key.PubKey);

            Directory.Delete(testPath, true);
        }
    }
}

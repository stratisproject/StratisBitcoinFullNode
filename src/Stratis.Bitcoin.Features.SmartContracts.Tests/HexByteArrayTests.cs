using System;
using System.Collections.Generic;
using System.Text;
using DBreeze.Utils;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class HexByteArrayTests
    {
        [Fact]
        public void HexString_ToByteArray_AndBack()
        {
            string hex = "08AFBC56";
            byte[] bytes = hex.HexToByteArray();
            Assert.Equal(4, bytes.Length);
            Assert.Equal(0x08, bytes[0]);
            Assert.Equal(0xAF, bytes[1]);
            Assert.Equal(0xBC, bytes[2]);
            Assert.Equal(0x56, bytes[3]);
            string hexFromByteArray = bytes.ToHexFromByteArray();
            Assert.Equal(hex, hexFromByteArray);
        }

        [Fact]
        public void HexString_ToByteArray_AndBack_LowerCase()
        {
            string hex = "08afbc56";
            byte[] bytes = hex.HexToByteArray();
            Assert.Equal(4, bytes.Length);
            Assert.Equal(0x08, bytes[0]);
            Assert.Equal(0xAF, bytes[1]);
            Assert.Equal(0xBC, bytes[2]);
            Assert.Equal(0x56, bytes[3]);
            string hexFromByteArray = bytes.ToHexFromByteArray();
            // Comes back identical but always upper case
            Assert.Equal(hex.ToUpper(), hexFromByteArray);
        }

        [Fact]
        public void HexString_ToByteArray_AndBack_With0x()
        {
            string hex = "0x08AFBC56";
            byte[] bytes = hex.HexToByteArray();
            Assert.Equal(4, bytes.Length);
            Assert.Equal(0x08, bytes[0]);
            Assert.Equal(0xAF, bytes[1]);
            Assert.Equal(0xBC, bytes[2]);
            Assert.Equal(0x56, bytes[3]);
            string hexFromByteArray = bytes.ToHexFromByteArray();
            // Comes back identical but with 0x
            Assert.Equal(hex, "0x" + hexFromByteArray);
        }

        [Fact]
        public void HexString_ToByteArray_InvalidFails()
        {
            string hex = "08afbc567"; // Uneven number of chars.
            Assert.ThrowsAny<Exception>(() => hex.ToByteArrayFromHex());
        }
    }
}

using Stratis.Bitcoin.Configuration;
using Xunit;

namespace Stratis.Bitcoin.Tests.NodeConfiguration
{
    public class TextFileConfigurationTests
    {

        [Fact]
        public void GetAllWithArrayArgs()
        {
            // Arrange
            var SUT = new TextFileConfiguration(new[] { "test" });
            // Act
            var parsed = SUT.GetAll("test");
            // Assert
            Assert.Equal("1", parsed[0]);
        }

        [Fact]
        public void GetAllMultipleKeysWithArrayArgsNoAssignment()
        {
            // Arrange
            var SUT = new TextFileConfiguration(new[] { "test", "-test" });
            // Act
            var parsed = SUT.GetAll("test");
            // Assert
            Assert.Equal(2, parsed.Length);
            Assert.Equal("1", parsed[0]);
            Assert.Equal("1", parsed[1]);
        }

        [Fact]
        public void GetAllMultipleKeysWithArrayArgsAssignment_Spaces()
        {
            // Arrange
            var SUT = new TextFileConfiguration(new[] { "test = testValue1", "-test = testValue2" });
            // Act
            var parsed = SUT.GetAll("test");
            // Assert
            Assert.Equal(2, parsed.Length);
            Assert.Equal("testValue1", parsed[0]);
            Assert.Equal("testValue2", parsed[1]);
        }

        [Fact]
        public void GetAllMultipleKeysWithArrayArgsAssignment_NoSpace()
        {
            // Arrange
            var SUT = new TextFileConfiguration(new[] { "test=testValue1", "-test=testValue2" });
            // Act
            var parsed = SUT.GetAll("test");
            // Assert
            Assert.Equal(2, parsed.Length);
            Assert.Equal("testValue1", parsed[0]);
            Assert.Equal("testValue2", parsed[1]);
        }

        [Fact]
        public void GetAllSuccessMultipleKeysWithStringArgs()
        {
            // Arrange
            var SUT = new TextFileConfiguration("test = testValue \n\r -test = testValue2");
            // Act
            var parsed = SUT.GetAll("test");
            // Assert
            Assert.Equal(2, parsed.Length);
            Assert.Equal("testValue", parsed[0]);
            Assert.Equal("testValue2", parsed[1]);
        }
    }
}

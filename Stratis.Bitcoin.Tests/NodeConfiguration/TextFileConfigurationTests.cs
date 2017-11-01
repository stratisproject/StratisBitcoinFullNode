using Stratis.Bitcoin.Configuration;
using Xunit;

namespace Stratis.Bitcoin.Tests.NodeConfiguration
{
    public class TextFileConfigurationTests
    {
        /// <summary>
        /// Assert that command line arguments with no value assigned default to "1".
        /// </summary>
        [Fact]
        public void GetAllWithArrayArgs()
        {
            // Arrange
            var textFileConfiguration = new TextFileConfiguration(new[] { "test" });
            // Act
            string[] result = textFileConfiguration.GetAll("test");
            // Assert
            Assert.Equal("1", result[0]);
        }

        /// <summary>
        /// Assert that we can get all the default values of command line arguments with or without a dash prefixing the key.
        /// </summary>
        [Fact]
        public void GetAllWithArrayArgsNoAssignment()
        {
            // Arrange
            var textFileConfiguration = new TextFileConfiguration(new[] { "test", "-test" });
            // Act
            string[] result = textFileConfiguration.GetAll("test");
            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("1", result[1]);
        }

        /// <summary>
        /// Assert that the parsing of command line arguments does not support spaces on each side of the = sign.
        /// </summary>
        [Fact]
        public void FailsToGetAllKeysWithArrayArgsAssignmentWithSpaces()
        {
            // Arrange
            var textFileConfiguration = new TextFileConfiguration(new[] { "test = testValue1", "-test = testValue2" });
            // Act
            string[] result = textFileConfiguration.GetAll("test");
            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// Assert that we can get all the assigned values of command line arguments with or without a dash prefixing the key.
        /// </summary>
        [Fact]
        public void GetAllKeysWithArrayArgsAssignment_NoSpace_()
        {
            // Arrange
            var textFileConfiguration = new TextFileConfiguration(new[] { "test=testValue1", "-test=testValue2" });
            // Act
            string[] result = textFileConfiguration.GetAll("test");
            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal("testValue1", result[0]);
            Assert.Equal("testValue2", result[1]);
        }

        /// <summary>
        /// Assert that we can get all the assigned values of arguments contained in a file with or without a dash prefixing the key.
        /// </summary>
        [Fact]
        public void GetAllSuccessMultipleKeysWithStringArgs()
        {
            // Arrange
            var textFileConfiguration = new TextFileConfiguration("test = testValue \n\r -test = testValue2");
            // Act
            string[] result = textFileConfiguration.GetAll("test");
            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal("testValue", result[0]);
            Assert.Equal("testValue2", result[1]);
        }

        /// <summary>
        /// Assert that we can pass mime-encoded-data as values
        /// </summary>
        [Fact]
        public void GetMimeValueWithFileString()
        {
            // Arrange
            var textFileConfiguration = new TextFileConfiguration("azurekey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==");
            // Act
            string[] result = textFileConfiguration.GetAll("azurekey");
            // Assert
            Assert.Single(result);
            Assert.Equal("Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==", result[0]);
        }

        /// <summary>
        /// Assert that we can pass mime-encoded-data as values
        /// </summary>
        [Fact]
        public void GetMimeValueWithStringArgs()
        {
            // Arrange
            var textFileConfiguration = new TextFileConfiguration(new string[] { "azurekey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==" });
            // Act
            string[] result = textFileConfiguration.GetAll("azurekey");
            // Assert
            Assert.Single(result);
            Assert.Equal("Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==", result[0]);
        }
    }
}

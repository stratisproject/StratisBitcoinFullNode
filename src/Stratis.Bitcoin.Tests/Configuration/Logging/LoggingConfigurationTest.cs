using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.Configuration.Logging
{
    public class LoggingConfigurationTest
    {
        /// <summary>
        /// The test will verify the bug #1307, but does not test the implementation directly.
        /// </summary>
        [Fact]
        public void PartialPathShouldBePrefixed()
        {
            var logPath = "StratStore1";

            var path = Path.IsPathRooted(logPath) ? string.Empty : Directory.GetCurrentDirectory();

            Assert.False(string.IsNullOrWhiteSpace(path));

            var fileName = Path.Combine(path, logPath, "node.txt");

            // Should not be equal, should have been rooted.
            Assert.NotEqual(Path.Combine(logPath, "node.txt"), fileName);

            // Check if the path is rooted.
            Assert.True(Path.IsPathRooted(path));
        }

        /// <summary>
        /// The test will verify the bug #1307, but does not test the implementation directly.
        /// </summary>
        [Fact]
        public void FullPathShouldNotBePrefixed()
        {
            var logPath = Path.GetTempPath();

            var path = Path.IsPathRooted(logPath) ? string.Empty : Directory.GetCurrentDirectory();

            Assert.Equal(string.Empty, path);

            var fileName = Path.Combine(path, logPath, "node.txt");

            // Should be equal and not changed, since it was already rooted.
            Assert.Equal(Path.Combine(logPath, "node.txt"), fileName);
        }
    }
}

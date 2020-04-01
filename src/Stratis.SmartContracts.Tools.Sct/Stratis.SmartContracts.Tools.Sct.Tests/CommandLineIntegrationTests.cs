using Stratis.SmartContracts.Tools.Sct.Tests.Utils;
using Xunit;

namespace Stratis.SmartContracts.Tools.Sct.Tests
{
    public class CommandLineIntegrationTests
    {
        [Fact]
        public void SingleFileCompilationWithByteCode()
        {
            using (var consoleOutput = new ConsoleOutput())
            {
                string[] args = new string[]
                {
                    "validate",
                    "Contracts/Single/DigitalLocker.cs",
                    "-sb"
                };
                Program.Main(args);

                string consoleText = consoleOutput.GetOuput();

                Assert.Contains("Compilation OK", consoleText);
                Assert.Contains("ByteCode", consoleText);
            }
        }

        [Fact]
        public void FileNotFoundFriendlyError()
        {
            using (var consoleOutput = new ConsoleOutput())
            {
                string[] args = new string[]
                {
                    "validate",
                    "NonExistent.cs",
                    "-sb"
                };
                Program.Main(args);

                string consoleText = consoleOutput.GetOuput();

                Assert.Contains("No file or directory NonExistent.cs exists.", consoleText);
            }
        }

        [Fact]
        public void DirectoryNotFoundFriendlyError()
        {
            using (var consoleOutput = new ConsoleOutput())
            {
                string[] args = new string[]
                {
                    "validate",
                    "NonExistentDirectory",
                    "-sb"
                };
                Program.Main(args);

                string consoleText = consoleOutput.GetOuput();

                Assert.Contains("No file or directory NonExistentDirectory exists.", consoleText);
            }
        }

        [Fact]
        public void MultipleFileCompilationWithByteCode()
        {
            using (var consoleOutput = new ConsoleOutput())
            {
                string[] args = new string[]
                {
                    "validate",
                    "Contracts/Multiple",
                    "-sb"
                };
                Program.Main(args);

                string consoleText = consoleOutput.GetOuput();

                Assert.Contains("Compilation OK", consoleText);
                Assert.Contains("ByteCode", consoleText);
            }
        }
    }
}

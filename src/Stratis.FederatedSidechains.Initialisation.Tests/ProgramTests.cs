using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Stratis.FederatedSidechains.Initialisation.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void RunWithNoArgsShouldPointsToDefaultFile()
        {
            var program = new Program();
            program.Run(null);
            program.ConfigFile.Should().Be(Program.DefaultConfigFile);
        }

        [Fact]
        public void RunWithArgsShouldPointsToFileInFirstArgument()
        {
            var args = new [] { @"hello\world.json"};
            var expectedFileInfo = new FileInfo(args.First());

            var program = new Program();
            program.Run(args);
            program.ConfigFile.FullName.Should().Be(expectedFileInfo.FullName);
        }
    }
}

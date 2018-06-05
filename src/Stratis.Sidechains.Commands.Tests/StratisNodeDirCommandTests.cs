using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using Xunit;
using FluentAssertions;

namespace Stratis.Sidechains.Commands.Tests
{
    public class StratisNodeDirCommandTests : ScriptBasedTest
    {
        [Fact]
        public void GetNodeDir()
        {
            var results = RunWorkingScript("getNodeDir.ps1");
            var nodeDir = results[0].ToString();
            nodeDir.Should().EndWith("GetNodeDir");
        }
    }
}

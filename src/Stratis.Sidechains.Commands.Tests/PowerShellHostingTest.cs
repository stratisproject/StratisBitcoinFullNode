using System.Management.Automation;
using FluentAssertions;
using Xunit;

namespace Stratis.Sidechains.Commands.Tests
{
    public class PowerShellHostingTest
    {
        [Fact]
        public void HelloWorld()
        {
            using (var ps = PowerShell.Create())
            {
                var result = ps.AddScript(@"Write-Output ""Hello World""").Invoke();
                ps.HadErrors.Should().BeFalse();
                result[0].ToString().Should().Be("Hello World");

                var executionDir = ps.AddScript("Convert-Path .").Invoke();
                result[0].ToString().Should().NotBeNull();

            }
        }
    }
}

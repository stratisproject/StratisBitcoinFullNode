using FluentAssertions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SignalR.Tests
{
    public class SignalRSettingsTests
    {
        [Fact]
        public void SignalRSettings_Should_Have_Default_Values_When_Not_Specified_In_NodeSettings()
        {
            var signalRSettings = new SignalRSettings(NodeSettings.Default(new StratisRegTest()));
            signalRSettings.HubRoute.Should().Be(SignalRSettings.DefaultSignalRHubRoute);
            signalRSettings.Port.Should().Be(SignalRSettings.DefaultSignalRPort);
        }

        [Fact]
        public void SignalRSettings_Should_Have_Overridden_Hub_When_Found_In_NodeSettings()
        {
            var hubName = "Hubert";
            NodeSettings nodeSettings = new NodeSettings(new StratisRegTest(),
                args: new[] { $"-{SignalRSettings.SignalRHubRouteParam}={hubName}" });

            var signalRSettings = new SignalRSettings(nodeSettings);

            signalRSettings.HubRoute.Should().NotBe(SignalRSettings.DefaultSignalRHubRoute);
            signalRSettings.HubRoute.Should().Be(hubName);

            signalRSettings.Port.Should().Be(SignalRSettings.DefaultSignalRPort);
        }

        [Fact]
        public void SignalRSettings_Should_Have_Overridden_Port_When_Found_In_NodeSettings()
        {
            var newPort = 12345;
            NodeSettings nodeSettings = new NodeSettings(new StratisRegTest(),
                args: new[] { $"-{SignalRSettings.SignalRPortParam}={newPort}" });

            var signalRSettings = new SignalRSettings(nodeSettings);

            signalRSettings.Port.Should().NotBe(SignalRSettings.DefaultSignalRPort);
            signalRSettings.Port.Should().Be(newPort);

            signalRSettings.HubRoute.Should().Be(SignalRSettings.DefaultSignalRHubRoute);
        }

        [Fact]
        public void SignalRSettings_Should_Have_Overridden_Port_And_Hub_When_Found_In_NodeSettings()
        {
            var newPort = 12366;
            var hubName = "Hubertzz";
            NodeSettings nodeSettings = new NodeSettings(new StratisRegTest(),
                args: new[] {
                      $"-{SignalRSettings.SignalRPortParam}={newPort}",
                      $"-{SignalRSettings.SignalRHubRouteParam}={hubName}"
                  });

            var signalRSettings = new SignalRSettings(nodeSettings);

            signalRSettings.Port.Should().NotBe(SignalRSettings.DefaultSignalRPort);
            signalRSettings.Port.Should().Be(newPort);

            signalRSettings.HubRoute.Should().NotBe(SignalRSettings.DefaultSignalRHubRoute);
            signalRSettings.HubRoute.Should().Be(hubName);
        }
    }
}

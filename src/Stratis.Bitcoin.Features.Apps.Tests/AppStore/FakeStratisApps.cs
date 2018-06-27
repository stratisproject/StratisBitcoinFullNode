using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps.Tests.AppStore
{
    public class FakeStratisApp1 : IStratisApp
    {
        public string DisplayName { get; } = "app1";
    }

    public class FakeStratisApp2 : IStratisApp
    {
        public string DisplayName { get; } = "app2";
    }
}

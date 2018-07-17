namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IStratisApp
    {
        string DisplayName { get; set; }

        string Location { get; set; }

        string WebRoot { get; set; }

        string Address { get; set; }

        bool IsSinglePageApp { get; }
    }
}

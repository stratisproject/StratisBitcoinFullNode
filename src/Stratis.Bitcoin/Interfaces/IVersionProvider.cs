namespace Stratis.Bitcoin.Interfaces
{
    public interface IVersionProvider
    {
        /// <summary>
        /// Returns an overridden version for the particular implementation.
        /// </summary>
        string GetVersion();
    }
}
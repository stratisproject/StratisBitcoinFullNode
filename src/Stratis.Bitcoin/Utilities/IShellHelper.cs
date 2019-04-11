namespace Stratis.Bitcoin.Utilities
{
    public interface IShellHelper
    {
        string RunCommand(string command, string args, bool wait = false, int timeout = 2000);
    }
}

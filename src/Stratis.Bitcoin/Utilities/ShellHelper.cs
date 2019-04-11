using System.Diagnostics;

namespace Stratis.Bitcoin.Utilities
{
    public class ShellHelper : IShellHelper
    {
        public string RunCommand(string command, string args, bool wait = false, int timeout = 2000)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = wait,
                RedirectStandardError = wait,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!wait)
            {
                Process.Start(startInfo);
                return string.Empty;
            }
            else
            {
                var process = Process.Start(startInfo);

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit(timeout);

                if (string.IsNullOrEmpty(error))
                {
                    return output;
                }
                else
                {
                    return error;
                }
            }
        }
    }
}

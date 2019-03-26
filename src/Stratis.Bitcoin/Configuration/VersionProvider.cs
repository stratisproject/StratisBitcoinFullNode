using System;
using System.Text.RegularExpressions;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Configuration
{
    public class VersionProvider : IVersionProvider
    {
        public string GetVersion()
        {
            Match match = Regex.Match(this.GetType().AssemblyQualifiedName, "Version=([0-9]+)(\\.([0-9]+)|)(\\.([0-9]+)|)(\\.([0-9]+)|)");
            string major = match.Groups[1].Value;
            string minor = match.Groups[3].Value;
            string build = match.Groups[5].Value;
            string revision = match.Groups[7].Value;

            if (revision == "0")
                return $"{major}.{minor}.{build}";

            return $"{major}.{minor}.{build}.{revision}";
        }
    }
}
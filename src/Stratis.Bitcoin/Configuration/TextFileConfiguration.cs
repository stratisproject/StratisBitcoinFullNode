using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Configuration
{
    /// <summary>
    /// Exception that is used when a problem in command line or configuration file configuration is found.
    /// </summary>
    public class ConfigurationException : Exception
    {
        /// <inheritdoc />
        public ConfigurationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Handling of application configuration.
    /// <para>
    /// This class provides the primary source of configuration for the application.
    /// It is used to include both the arguments from the command line as well as
    /// settings loaded from the configuration file.
    /// </para>
    /// </summary>
    public class TextFileConfiguration
    {
        /// <summary>Application command line arguments as a mapping of argument name to list of its values.</summary>
        private readonly Dictionary<string, List<string>> args;

        /// <summary>
        /// Initializes the instance of the object using command line arguments.
        /// </summary>
        /// <param name="args">Application command line arguments.</param>
        /// <remarks>Command line arguments are expected to come in form of Name=Value, where Name can be prefixed with '-'.</remarks>
        public TextFileConfiguration(string[] args)
        {
            this.args = new Dictionary<string, List<string>>();
            foreach (string arg in args)
            {
                // Split on the FIRST "=".
                // This will allow mime-encoded - data strings end in one or more "=" to be parsed.
                string[] splitted = arg.Split('=');
                string key = splitted[0];
                if (!key.StartsWith("-"))
                    key = "-" + key;

                if (splitted.Length > 1)
                    this.Add(key, string.Join("=", splitted.Skip(1)));
                else
                    this.Add(key, "1");
            }
        }

        /// <summary>
        /// Initializes the instance of the object using the configuration file contents.
        /// </summary>
        /// <param name="data">Contents of the configuration file to parse and extract arguments from.</param>
        public TextFileConfiguration(string data)
        {
            this.args = new Dictionary<string, List<string>>();
            int lineNumber = 0;
            // Process all lines, even if empty.
            foreach (string l in data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                // Track line numbers, also for empty lines.
                lineNumber++;
                string line = l.Trim();

                // From here onwards don't process empty or commented lines.
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Split on the FIRST "=".
                // This will allow mime-encoded - data strings end in one or more "=" to be parsed.
                string[] split = line.Split('=');
                if (split.Length == 1)
                    throw new FormatException("Line " + lineNumber + $": \"{l}\" : No value is set");

                // Add to dictionary. Trim spaces around keys and values.
                string key = split[0].Trim();
                if (!key.StartsWith("-"))
                    key = "-" + key;

                this.Add(key, string.Join("=", split.Skip(1)).Trim());
            }
        }

        /// <summary>
        /// Adds argument and its value to the argument list.
        /// <para>If the argument exists already in the list, the value is appended to the list of its values.</para>
        /// </summary>
        /// <param name="key">Name of the argument.</param>
        /// <param name="value">Argument value.</param>
        private void Add(string key, string value)
        {
            key = key.ToLowerInvariant();

            if (!this.args.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                this.args.Add(key, list);
            }

            list.Add(value);
        }

        /// <summary>
        /// Merges current instance of the configuration to the target instance.
        /// </summary>
        /// <param name="destination">Target instance to merge current instance into.</param>
        public void MergeInto(TextFileConfiguration destination)
        {
            foreach (KeyValuePair<string, List<string>> kv in this.args)
            {
                foreach (string v in kv.Value)
                    destination.Add(kv.Key, v);
            }
        }

        /// <summary>
        /// Retrieves all values of a specific argument name (where the name excludes the dash prefix).
        /// </summary>
        /// <param name="key">Name of the argument (excluding the dash prefix).</param>
        /// <param name="logger">The settings logger used to log the value. Logs on Debug level.</param>
        /// <returns>Values for the specified argument.</returns>
        public string[] GetAll(string key, ILogger logger = null)
        {
            key = key.ToLowerInvariant();

            // Get the values with the - prefix.
            if (!this.args.TryGetValue($"-{key}", out List<string> values))
                values = new List<string>();

            logger?.LogDebug("{0} entries were returned for the key '{1}': {2}",
                values.Count, key, string.Join(",", values.Select(str => $"'{str}'")));

            return values.ToArray();
        }

        /// <summary>
        /// Gets typed value for a specific argument or a default value.
        /// </summary>
        /// <typeparam name="T">Type of the argument value.</typeparam>
        /// <param name="key">Name of the argument.</param>
        /// <param name="defaultValue">Default value to return if no argument value is defined.</param>
        /// <param name="logger">The settings logger to use to log the value. Logs on Debug level.</param>
        /// <returns>Value of the argument or a default value if no value was set.</returns>
        public T GetOrDefault<T>(string key, T defaultValue, ILogger logger = null)
        {
            key = key.ToLowerInvariant();

            if (!this.args.TryGetValue($"-{key}", out List<string> values))
            {
                logger?.LogDebug("Default value '{0}' was selected for the key '{1}'.", defaultValue, key);
                return defaultValue;
            }

            try
            {
                var value = this.ConvertValue<T>(values[0]);
                logger?.LogDebug("Value '{0}' was loaded for the key '{1}'.", value, key);
                return value;
            }
            catch (FormatException)
            {
                throw new ConfigurationException($"Key {key} should be of type {typeof(T).Name}.");
            }
        }

        /// <summary>
        /// Converts a string to a typed value.
        /// </summary>
        /// <typeparam name="T">Type of the value to convert the string to.</typeparam>
        /// <param name="str">String representation of the value.</param>
        /// <returns>Typed value.</returns>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> is not supported type.</exception>
        /// <exception cref="FormatException">Thrown if the string does not represent a valid value of <typeparamref name="T"/>.</exception>
        private T ConvertValue<T>(string str)
        {
            if (typeof(T) == typeof(bool))
            {
                var trueValues = new[] { "1", "true" };
                var falseValues = new[] { "0", "false" };

                if (trueValues.Contains(str, StringComparer.OrdinalIgnoreCase))
                    return (T)(object)true;

                if (falseValues.Contains(str, StringComparer.OrdinalIgnoreCase))
                    return (T)(object)false;

                throw new FormatException();
            }

            if (typeof(T) == typeof(string))
            {
                return (T)(object)str;
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)int.Parse(str, CultureInfo.InvariantCulture);
            }

            if (typeof(T) == typeof(Int64))
            {
                return (T)(object)Int64.Parse(str, CultureInfo.InvariantCulture);
            }

            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)ulong.Parse(str, CultureInfo.InvariantCulture);
            }

            if (typeof(T) == typeof(Uri))
            {
                return (T)(object)new Uri(str);
            }

            if (typeof(T) == typeof(uint256))
            {
                uint256 value = null;
                if (str != "0" && !uint256.TryParse(str, out value))
                    throw new FormatException($"Cannot parse uint256 from {str}.");
                return (T)(object)value;
            }

            throw new NotSupportedException("Configuration value does not support type " + typeof(T).Name);
        }
    }
}

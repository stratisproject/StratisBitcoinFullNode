using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Configuration
{
	public class ConfigurationException : Exception
	{
		public ConfigurationException(string message) : base(message)
		{

		}
	}
	public class TextFileConfiguration
	{
		private Dictionary<string, List<string>> _Args;

		public TextFileConfiguration(string[] args)
		{
			_Args = new Dictionary<string, List<string>>();
			foreach(var arg in args)
			{
				var splitted = arg.Split('=');
				if(splitted.Length == 2)
					Add(splitted[0], splitted[1]);
				if(splitted.Length == 1)
					Add(splitted[0], "1");
			}
		}

		private void Add(string key, string value)
		{
			List<string> list;
			if(!_Args.TryGetValue(key, out list))
			{
				list = new List<string>();
				_Args.Add(key, list);
			}
			list.Add(value);
		}

		public void MergeInto(TextFileConfiguration destination)
		{
			foreach(var kv in _Args)
			{
				foreach(var v in kv.Value)
					destination.Add(kv.Key, v);
			}
		}

		public TextFileConfiguration(Dictionary<string, List<string>> args)
		{
			this._Args = args;
		}

        public static TextFileConfiguration Parse(string data)
        {
            return new TextFileConfiguration(data);
        }

		public TextFileConfiguration(string data)
		{
            this._Args = new Dictionary<string, List<string>>();
            int lineNumber = 0;
            // Process all lines, even if empty
			foreach(var l in data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
			{
                // Track line numbers, also for empty lines..
                lineNumber++;
				var line = l.Trim();
                // From here onwards don't process empty or commented lines
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;
                // Split on '='
                string[] split = line.Split('=');
                if (split.Length == 1)
                    throw new FormatException("Line " + lineNumber + $": \"{l}\" : No value is set");
                if (split.Length > 2)
                    throw new FormatException("Line " + lineNumber + $": \"{l}\" : Only one '=' was expected");
                // Add to dictionary. Trim spaces around keys and values
                Add(split[0].Trim(), split[1].Trim());
			}
		}

		public bool Contains(string key)
		{
			List<string> values;
			return _Args.TryGetValue(key, out values) 
				|| _Args.TryGetValue($"-{key}", out values);
		}
		public string[] GetAll(string key)
		{
			List<string> values;
			if(!_Args.TryGetValue(key, out values))
				if (!_Args.TryGetValue($"-{key}", out values))
					return new string[0];
			return values.ToArray();
		}

		public T GetOrDefault<T>(string key, T defaultValue)
		{
			List<string> values;
			if(!_Args.TryGetValue(key, out values))
				if (!_Args.TryGetValue($"-{key}", out values))
					return defaultValue;

			if(values.Count != 1)
				throw new ConfigurationException("Duplicate value for key " + key);
			try
			{
				return ConvertValue<T>(values[0]);
			}
			catch(FormatException) { throw new ConfigurationException("Key " + key + " should be of type " + typeof(T).Name); }
		}

		private T ConvertValue<T>(string str)
		{
			if(typeof(T) == typeof(bool))
			{
				var trueValues = new[] { "1", "true" };
				var falseValues = new[] { "0", "false" };
				if(trueValues.Contains(str, StringComparer.OrdinalIgnoreCase))
					return (T)(object)true;
				if(falseValues.Contains(str, StringComparer.OrdinalIgnoreCase))
					return (T)(object)false;
				throw new FormatException();
			}
			else if(typeof(T) == typeof(string))
				return (T)(object)str;
			else if(typeof(T) == typeof(int))
			{
				return (T)(object)int.Parse(str, CultureInfo.InvariantCulture);
			}
			else if (typeof(T) == typeof(Uri))
			{
				return (T)(object)new Uri(str);
			}
			else
			{
				throw new NotSupportedException("Configuration value does not support time " + typeof(T).Name);
			}
		}

		//public static String CreateDefaultConfiguration(Network network)
		//{
		//	StringBuilder builder = new StringBuilder();
		//	builder.AppendLine("#rpc.url=http://localhost:" + network.RPCPort + "/");
		//	builder.AppendLine("#rpc.user=bitcoinuser");
		//	builder.AppendLine("#rpc.password=bitcoinpassword");
		//	builder.AppendLine("#rpc.cookiefile=yourbitcoinfolder/.cookie");
		//	return builder.ToString();
		//}

		//public static String CreateClientDefaultConfiguration(Network network)
		//{
		//	StringBuilder builder = new StringBuilder();
		//	builder.AppendLine("#rpc.url=http://localhost:" + network.RPCPort + "/");
		//	builder.AppendLine("#rpc.user=bitcoinuser");
		//	builder.AppendLine("#rpc.password=bitcoinpassword");
		//	builder.AppendLine("#rpc.cookiefile=yourbitcoinfolder/.cookie");
		//	return builder.ToString();
		//}
	}
}

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
			Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
			var lines = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
			int lineCount = -1;
			foreach(var l in lines)
			{
				lineCount++;
				var line = l.Trim();
				if(string.IsNullOrEmpty(line) || line.StartsWith("#"))
					continue;
				var split = line.Split('=');
				if(split.Length == 0)
					continue;
				if(split.Length == 1)
					throw new FormatException("Line " + lineCount + ": No value are set");

				var key = split[0];
				List<string> values;
				if(!result.TryGetValue(key, out values))
				{
					values = new List<string>();
					result.Add(key, values);
				}
				var value = String.Join("=", split.Skip(1).ToArray());
				values.Add(value);
			}
			return new TextFileConfiguration(result);
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

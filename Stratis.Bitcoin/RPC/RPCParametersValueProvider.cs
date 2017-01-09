using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC
{
	public class RPCParametersValueProvider : IValueProvider, IValueProviderFactory
	{
		public RPCParametersValueProvider()
		{

		}
		public RPCParametersValueProvider(ValueProviderFactoryContext context)
		{
			this.context = context;
		}
		ValueProviderFactoryContext context;
		public bool ContainsPrefix(string prefix)
		{
			return GetValueCore(prefix) != null;
		}

		public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
		{
			context.ValueProviders.Clear();
			context.ValueProviders.Add(new RPCParametersValueProvider(context));
			return Task.CompletedTask;
		}

		string GetValueCore(string key)
		{
			var req = (JObject)context.ActionContext.RouteData.Values["req"];
			if(req == null)
				return null;
			var parameter = context.ActionContext.ActionDescriptor.Parameters.FirstOrDefault(p => p.Name == key);
			if(parameter == null)
				return null;
			var index = context.ActionContext.ActionDescriptor.Parameters.IndexOf(parameter);
			var parameters = (JArray)req["params"];
			if(index < 0 || index >= parameters.Count)
				return null;
			var jtoken = parameters[index];
			return jtoken == null ? null : jtoken.ToString();
		}

		public ValueProviderResult GetValue(string key)
		{
			//context.ActionContext.ActionDescriptor.Parameters.First().BindingInfo.
			return new ValueProviderResult(new Microsoft.Extensions.Primitives.StringValues(GetValueCore(key)));
		}
	}
}

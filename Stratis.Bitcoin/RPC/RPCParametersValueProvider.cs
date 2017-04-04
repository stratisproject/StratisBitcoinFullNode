using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.RPC
{
	public interface IRPCParametersValueProvider : IValueProvider, IValueProviderFactory
	{
	}

	public class RPCParametersValueProvider : IRPCParametersValueProvider
	{
		public RPCParametersValueProvider()
		{
			
		}

		public RPCParametersValueProvider(ValueProviderFactoryContext context)
		{
			Guard.NotNull(context, nameof(context));

			this.context = context;
		}

		ValueProviderFactoryContext context;
		public bool ContainsPrefix(string prefix)
		{
			Guard.NotNull(prefix, nameof(prefix));

			return GetValueCore(prefix) != null;
		}

		public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
		{
			Guard.NotNull(context, nameof(context));
			Guard.NotNull(context.ValueProviders, nameof(context.ValueProviders));

			context.ValueProviders.Clear();
			context.ValueProviders.Add(new RPCParametersValueProvider(context));
			return Task.CompletedTask;
		}

		public ValueProviderResult GetValue(string key)
		{
			Guard.NotNull(key, nameof(key));

			//context.ActionContext.ActionDescriptor.Parameters.First().BindingInfo.
			return new ValueProviderResult(new Microsoft.Extensions.Primitives.StringValues(GetValueCore(key)));
		}

		private string GetValueCore(string key)
		{
			if (key == null)
				return null;
			if (context.ActionContext.RouteData == null)
				return null;
			if (context.ActionContext.RouteData.Values == null || context.ActionContext.RouteData.Values.Count == 0)
				return null;
			var req = context.ActionContext.RouteData.Values["req"] as JObject;
			if(req == null)
				return null;
			if (context.ActionContext.ActionDescriptor == null || context.ActionContext.ActionDescriptor.Parameters == null)
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
	}
}

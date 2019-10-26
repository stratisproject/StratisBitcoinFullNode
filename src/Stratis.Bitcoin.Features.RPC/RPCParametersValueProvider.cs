using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.RPC.ModelBinders;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC
{
    public interface IRPCParametersValueProvider : IValueProvider, IValueProviderFactory
    {
    }

    public class RPCParametersValueProvider : IRPCParametersValueProvider
    {
        private ValueProviderFactoryContext context;

        public RPCParametersValueProvider()
        {
        }

        public RPCParametersValueProvider(ValueProviderFactoryContext context)
        {
            Guard.NotNull(context, nameof(context));

            this.context = context;
        }

        public bool ContainsPrefix(string prefix)
        {
            Guard.NotNull(prefix, nameof(prefix));

            return this.GetValueCore(prefix) != null;
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
            return new ValueProviderResult(new Microsoft.Extensions.Primitives.StringValues(this.GetValueCore(key)));
        }

        private string GetValueCore(string key)
        {
            if (key == null)
                return null;

            var actionContext = this.context.ActionContext;
            if (actionContext.RouteData?.Values == null || (actionContext.RouteData.Values.Count == 0))
                return null;

            var req = actionContext.RouteData.Values["req"] as JObject;
            if (req == null)
                return null;

            var actionParameters = actionContext.ActionDescriptor?.Parameters;
            ParameterDescriptor parameter = actionParameters?.FirstOrDefault(p => p.Name == key);

            if (parameter == null)
                return null;

            int index = actionParameters.IndexOf(parameter);

            var parameters = (JArray)req["params"];
            if (parameters == null)
            {
                var parameterInfo = (parameter as ControllerParameterDescriptor)?.ParameterInfo;
                return parameterInfo?.DefaultValue?.ToString();
            }

            if ((index < 0) || (index >= parameters.Count))
                return null;

            JToken jToken = parameters[index];
            string value = jToken?.ToString();

            if (parameter.ParameterType != typeof(bool)) return value;

            bool hasIntToBoolAttribute = (parameter as ControllerParameterDescriptor)?.ParameterInfo?.CustomAttributes?.Any(a => a.AttributeType == typeof(IntToBoolAttribute)) ?? false;
            if (hasIntToBoolAttribute && int.TryParse(value, out int number))
            {
                return number != 0 ? "true" : "false";
            }

            return value;
        }
    }
}

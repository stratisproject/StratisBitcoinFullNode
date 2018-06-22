using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;

namespace Stratis.Bitcoin.Features.RPC.ModelBinders
{
    public class DestinationModelBinder : IModelBinder, IModelBinderProvider
    {
        public DestinationModelBinder()
        {
        }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (!SupportType(bindingContext.ModelType))
            {
                return Task.CompletedTask;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

            string key = val.FirstValue;
            if (key == null)
            {
                return Task.CompletedTask;
            }

            var network = (Network)bindingContext.HttpContext.RequestServices.GetService(typeof(Network));
            //TODO: Use var data = Network.Parse(key, network); when NBitcoin is updated to latest version
            BitcoinAddress data = BitcoinAddress.Create(key, network);
            if (!bindingContext.ModelType.IsInstanceOfType(data))
            {
                throw new FormatException("Invalid destination type");
            }
            bindingContext.Result = ModelBindingResult.Success(data);
            return Task.CompletedTask;
        }

        private static bool SupportType(Type type)
        {
            return (typeof(Base58Data).GetTypeInfo().IsAssignableFrom(type) ||
                   typeof(IDestination).GetTypeInfo().IsAssignableFrom(type));
        }

        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (SupportType(context.Metadata.ModelType))
                return this;
            return null;
        }
    }
}

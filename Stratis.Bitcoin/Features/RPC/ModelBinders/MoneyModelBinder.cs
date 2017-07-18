using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;

namespace Stratis.Bitcoin.Features.RPC.ModelBinders
{
    public class MoneyModelBinder : IModelBinder, IModelBinderProvider
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if(bindingContext.ModelType != typeof(Money))
            {
                return TaskCache.CompletedTask;
            }

            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if(val == null)
            {
                return TaskCache.CompletedTask;
            }

            string key = val.FirstValue as string;
            if(key == null)
            {
                return TaskCache.CompletedTask;
            }
            return Task.FromResult(Money.Parse(key));
        }

        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if(context.Metadata.ModelType == typeof(Money))
                return this;
            return null;
        }
    }
}

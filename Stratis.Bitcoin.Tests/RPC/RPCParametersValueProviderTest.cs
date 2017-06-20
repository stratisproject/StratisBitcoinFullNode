using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.RPC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.RPC
{
    [TestClass]
	public class RPCParametersValueProviderTest
	{
		private ValueProviderFactoryContext context;
		private RPCParametersValueProvider provider;
		private ActionContext actionContext;

        [TestInitialize]
		public void Initialize()
		{
			this.actionContext = new ActionContext();
			this.context = new ValueProviderFactoryContext(this.actionContext);
			this.provider = new RPCParametersValueProvider(this.context);
		}

		[TestMethod]
		public void CreateValueProviderAsyncCreatesValueProviderForContext()
		{
			var task = this.provider.CreateValueProviderAsync(this.context);
			task.Wait();

			Assert.AreEqual(1, this.context.ValueProviders.Count);
			Assert.IsTrue(this.context.ValueProviders[0] is RPCParametersValueProvider);
		}

		[TestMethod]
		public void ContainsPrefixWithoutRouteDataReturnsFalse()
		{
			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixWithEmptyRouteDataReturnsFalse()
		{
			this.actionContext.RouteData = new RouteData();

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixWithReqValueWithoutParameterInRouteDataReturnsFalse()
		{
			this.actionContext.RouteData = new RouteData();
			this.actionContext.RouteData.Values.Add("req", "");

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixWithoutActionDescriptorReturnsFalse()
		{
			this.actionContext.RouteData = new RouteData();
			this.actionContext.RouteData.Values.Add("req", new JObject());
			this.actionContext.ActionDescriptor = null;

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixWithoutActionDescriptorParametersReturnsFalse()
		{
			this.actionContext.RouteData = new RouteData();
			this.actionContext.RouteData.Values.Add("req", new JObject());
			this.actionContext.ActionDescriptor = new ActionDescriptor();
			this.actionContext.ActionDescriptor.Parameters = null;

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixWithoutKeyInParametersReturnsFalse()
		{
			this.actionContext.RouteData = new RouteData();
			this.actionContext.RouteData.Values.Add("req", new JObject());
			this.actionContext.ActionDescriptor = new ActionDescriptor();
			this.actionContext.ActionDescriptor.Parameters = new List<ParameterDescriptor>();

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixIndexBeyondParametersReturnsFalse()
		{
			this.actionContext.RouteData = new RouteData();

			string obj = "{ \"params\" : [] }";
			this.actionContext.RouteData.Values.Add("req", JObject.Parse(obj));
			this.actionContext.ActionDescriptor = new ActionDescriptor();
			this.actionContext.ActionDescriptor.Parameters = new List<ParameterDescriptor>();
			this.actionContext.ActionDescriptor.Parameters.Add(new ParameterDescriptor() { Name = "1" });
			this.actionContext.ActionDescriptor.Parameters.Add(new ParameterDescriptor() { Name = "2" });
			this.actionContext.ActionDescriptor.Parameters.Add(new ParameterDescriptor() { Name = "rpc_" });

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixParameterNullReturnsFalse()
		{
			this.actionContext.RouteData = new RouteData();

			string obj = "{ \"params\" : [] }";
			this.actionContext.RouteData.Values.Add("req", JObject.Parse(obj));
			this.actionContext.ActionDescriptor = new ActionDescriptor();
			this.actionContext.ActionDescriptor.Parameters = new List<ParameterDescriptor>();
			this.actionContext.ActionDescriptor.Parameters.Add(new ParameterDescriptor() { Name = "rpc_" });

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsFalse(result);
		}

		[TestMethod]
		public void ContainsPrefixParameterNotNullReturnsTrue()
		{
			this.actionContext.RouteData = new RouteData();

			string obj = "{ \"params\" : [\"Yes\"] }";
			this.actionContext.RouteData.Values.Add("req", JObject.Parse(obj));
			this.actionContext.ActionDescriptor = new ActionDescriptor();
			this.actionContext.ActionDescriptor.Parameters = new List<ParameterDescriptor>();
			this.actionContext.ActionDescriptor.Parameters.Add(new ParameterDescriptor() { Name = "rpc_" });

			var result = this.provider.ContainsPrefix("rpc_");

			Assert.IsTrue(result);
		}

		[TestMethod]
		public void GetValueReturnsResultIfExists()
		{
			this.actionContext.RouteData = new RouteData();

			string obj = "{ \"params\" : [\"Yes\"] }";
			this.actionContext.RouteData.Values.Add("req", JObject.Parse(obj));
			this.actionContext.ActionDescriptor = new ActionDescriptor();
			this.actionContext.ActionDescriptor.Parameters = new List<ParameterDescriptor>();
			this.actionContext.ActionDescriptor.Parameters.Add(new ParameterDescriptor() { Name = "rpc_" });

			var result = this.provider.GetValue("rpc_");

			Assert.AreEqual("Yes", result.FirstValue);
		}

		[TestMethod]
		public void GetValueReturnsNullIfNotExists()
		{
			this.actionContext.RouteData = new RouteData();

			string obj = "{ \"params\" : [] }";
			this.actionContext.RouteData.Values.Add("req", JObject.Parse(obj));
			this.actionContext.ActionDescriptor = new ActionDescriptor();
			this.actionContext.ActionDescriptor.Parameters = new List<ParameterDescriptor>();
			this.actionContext.ActionDescriptor.Parameters.Add(new ParameterDescriptor() { Name = "rpc_" });

			var result = this.provider.GetValue("rpc_");

			Assert.AreEqual(null, result.FirstValue);
		}
	}
}

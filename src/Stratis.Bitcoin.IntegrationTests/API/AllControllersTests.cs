using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public class AllControllersTests
    {
        [Fact]
        public void AllPostMethodsShouldHaveBody()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("Stratis") || a.FullName.StartsWith("NBitcoin"));
            var controllers = assemblies.SelectMany(GetControllersInAssembly()).ToList();

            foreach (var controller in controllers)
            {
                var postMethods = controller.GetMethods()
                    .Where(IsHttpPostMethod())
                    .ToList();

                postMethods.ForEach(method => method.GetParameters()
                    .Count(HasAnyFromBodyAttribute())
                    .Should().BeGreaterOrEqualTo(1,
                    $"HttpPost method {controller.FullName}.{method.Name} should have at least one "
                    + $"[FromBody] parameter to prevent CORS calls from executing it. "
                    + $"Cf. https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS#Simple_requests"));
            }
        }

        private static Func<Assembly, IEnumerable<Type>> GetControllersInAssembly()
        {
            return a => a.GetTypes().Where(t => t.Implements(typeof(Controller)));
        }

        private static Func<MethodInfo, bool> IsHttpPostMethod()
        {
            return m => m.CustomAttributes.Any(a => a.AttributeType == typeof(HttpPostAttribute));
        }

        private static Func<ParameterInfo, bool> HasAnyFromBodyAttribute()
        {
            return p => p.CustomAttributes.Any(a => a.AttributeType == typeof(FromBodyAttribute));
        }
    }
}

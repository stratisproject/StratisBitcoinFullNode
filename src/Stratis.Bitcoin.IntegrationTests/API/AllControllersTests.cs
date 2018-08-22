using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Features.Api;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public class AllControllersTests
    {
        [Fact]
        public void AllPostMethodsShouldHaveBody()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var controllers = assemblies.SelectMany(a => a.GetTypes().Where(t => t.Implements(typeof(Controller))));

            foreach (var controller in controllers)
            {
                var postMethods = controller.GetMethods()
                    .Where(m => m.CustomAttributes.Any(a => a.AttributeType == typeof(HttpPostAttribute)))
                    .ToList();
                postMethods.ForEach(mi => mi.GetParameters().Count().Should().BeGreaterOrEqualTo(1, $"HttpPost method {mi.Name} should have at least one parameter to prevent CORS calls from executing it."));
            }
        }
    }
}

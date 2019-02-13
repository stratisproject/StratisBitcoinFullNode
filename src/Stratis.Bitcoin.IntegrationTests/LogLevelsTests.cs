using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Castle.Components.DictionaryAdapter;
using DBreeze.Utils;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using NSubstitute;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    /// <summary>
    /// This class tests the 'api/node/loglevels' endpoint.
    /// </summary>
    public class LogLevelsTests : IDisposable
    {
        private readonly Network network;

        private IList<LoggingRule> rules;

        public LogLevelsTests()
        {
            this.network = new StratisRegTest();
        }

        public void Dispose()
        {
            LogManager.Configuration.LoggingRules.Clear();
        }

        /// <summary>
        /// Creates a bunch of rules for testing.
        /// </summary>
        private void ConfigLogManager()
        {
            this.rules = LogManager.Configuration.LoggingRules;
            this.rules.Add(new LoggingRule("logging1", LogLevel.Info, new FileTarget("file1") { FileName = "file1.txt" }));
            this.rules.Add(new LoggingRule("logging2", LogLevel.Fatal, new FileTarget("file2") { FileName = "file2.txt" }));
            this.rules.Add(new LoggingRule("logging3", LogLevel.Trace, new FileTarget("file3") { FileName = "file3.txt" }));
        }

        [Fact]
        public async Task ChangeLogLevelWithNonExistantLoggerAsync()
        {
            string ruleName = "non-existant-rule";
            string logLevel = "debug";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                Func<Task> act = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request)
                    .ReceiveJson<string>();
              
                // Assert.
                var exception = act.Should().Throw<FlurlHttpException>().Which;
                var response = exception.Call.Response;

                ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                List<ErrorModel> errors = errorResponse.Errors;

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be($"Logger name `{ruleName}` doesn't exist.");
            }
        }

        [Fact]
        public async Task ChangeLogLevelWithNonExistantLogLevelAsync()
        {
            string ruleName = "logging1";
            string logLevel = "xxxxxxxx";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                Func<Task> act = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request)
                    .ReceiveJson<string>();

                // Assert.
                var exception = act.Should().Throw<FlurlHttpException>().Which;
                var response = exception.Call.Response;

                ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                List<ErrorModel> errors = errorResponse.Errors;

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be($"Failed converting {logLevel} to a member of NLog.LogLevel.");
            }
        }

        [Fact]
        public async Task ChangeLogLevelToLowerOrdinalAsync()
        {
            string ruleName = "logging2"; // Currently 'fatal'.
            string logLevel = "trace";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                HttpResponseMessage result = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request);

                // Assert.
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                this.rules = LogManager.Configuration.LoggingRules;
                this.rules.Single(r => r.LoggerNamePattern == ruleName).Levels.Should().ContainInOrder(new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal });
            }
        }

        [Fact]
        public async Task ChangeLogLevelToHigherOrdinalAsync()
        {
            string ruleName = "logging3"; // Currently 'trace'.
            string logLevel = "info";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                HttpResponseMessage result = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request);

                // Assert.
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                this.rules = LogManager.Configuration.LoggingRules;
                this.rules.Single(r => r.LoggerNamePattern == ruleName).Levels.Should().ContainInOrder(new[] { LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal });
            }
        }

        [Fact]
        public async Task ChangeLogLevelOfMultipleRulesAsync()
        {
            string ruleName1 = "logging1";
            string ruleName2 = "logging2";
            string ruleName3 = "logging3";
            string logLevel = "Error";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest>
                {
                    new LogRuleRequest { RuleName = ruleName1, LogLevel = logLevel },
                    new LogRuleRequest { RuleName = ruleName2, LogLevel = logLevel },
                    new LogRuleRequest { RuleName = ruleName3, LogLevel = logLevel }
                } };

                HttpResponseMessage result = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request);

                // Assert.
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                this.rules = LogManager.Configuration.LoggingRules;
                this.rules.Single(r => r.LoggerNamePattern == ruleName1).Levels.Should().ContainInOrder(new[] { LogLevel.Error, LogLevel.Fatal });
                this.rules.Single(r => r.LoggerNamePattern == ruleName2).Levels.Should().ContainInOrder(new[] { LogLevel.Error, LogLevel.Fatal });
                this.rules.Single(r => r.LoggerNamePattern == ruleName3).Levels.Should().ContainInOrder(new[] { LogLevel.Error, LogLevel.Fatal });
            }
        }
    }
}

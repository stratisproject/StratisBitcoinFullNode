using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public class AddressBookTests
    {
        private readonly Network network;

        public AddressBookTests()
        {
            this.network = new StratisRegTest();
        }

        [Fact]
        public async Task AddAnAddressBookEntry()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                // Act.
                AddressBookEntryModel newEntry = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label1", address = "TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu" })
                    .ReceiveJson<AddressBookEntryModel>();

                // Assert.
                // Check the address is in the address book.
                AddressBookModel addressBook = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook")
                    .GetJsonAsync<AddressBookModel>();

                addressBook.Addresses.Should().ContainSingle();
                addressBook.Addresses.Single().Label.Should().Be("label1");
                addressBook.Addresses.Single().Address.Should().Be("TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu");
            }
        }

        [Fact]
        public async Task AddAnAddressBookEntryWhenAnEntryAlreadyExists()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                // Add a first address.
                AddressBookEntryModel newEntry = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label1", address = "TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu" })
                    .ReceiveJson<AddressBookEntryModel>();
                
                // Act.
                // Add an entry with the same address and label already exist.
                Func<Task> firstAttempt = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label1", address = "TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu" })
                    .ReceiveJson<AddressBookEntryModel>();

                // Add an entry with the same address only already exist.
                Func<Task> secondAttempt = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label2", address = "TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu" })
                    .ReceiveJson<AddressBookEntryModel>();

                // Add an entry with the same label already exist.
                Func<Task> thirdAttempt = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label1", address = "TWMxjBk5bVdv8dhDJ645Z5RoxfrbRUJewa" })
                    .ReceiveJson<AddressBookEntryModel>();

                // Assert.
                var exception = firstAttempt.Should().Throw<FlurlHttpException>().Which;
                var response = exception.Call.Response;
                ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                List<ErrorModel> errors = errorResponse.Errors;
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be($"An entry with label 'label1' or address 'TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu' already exist in the address book.");

                exception = secondAttempt.Should().Throw<FlurlHttpException>().Which;
                response = exception.Call.Response;
                errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                errors = errorResponse.Errors;
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be($"An entry with label 'label2' or address 'TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu' already exist in the address book.");

                exception = thirdAttempt.Should().Throw<FlurlHttpException>().Which;
                response = exception.Call.Response;
                errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                errors = errorResponse.Errors;
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be($"An entry with label 'label1' or address 'TWMxjBk5bVdv8dhDJ645Z5RoxfrbRUJewa' already exist in the address book.");
            }
        }

        [Fact]
        public async Task RemoveAnAddressBookEntryWhenNoSuchEntryExists()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                // Act.
                // Add an entry with the same address and label already exist.
                Func<Task> act = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .SetQueryParams(new { label = "label1" })
                    .DeleteAsync()
                    .ReceiveJson<AddressBookEntryModel>();

                // Assert.
                var exception = act.Should().Throw<FlurlHttpException>().Which;
                var response = exception.Call.Response;
                ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                List<ErrorModel> errors = errorResponse.Errors;
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be("No item with label 'label1' was found in the address book.");
            }
        }

        [Fact]
        public async Task RemoveAnAddressBookEntryWhenAnEntryExists()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                // Add a first address.
                AddressBookEntryModel newEntry = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label1", address = "TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu" })
                    .ReceiveJson<AddressBookEntryModel>();

                // Check the address is in the address book.
                AddressBookModel addressBook = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook")
                    .SetQueryParams(new { label = "label1" })
                    .GetJsonAsync<AddressBookModel>();

                addressBook.Addresses.Should().ContainSingle();
                addressBook.Addresses.Single().Label.Should().Be("label1");

                // Act.
                AddressBookEntryModel entryRemoved = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .SetQueryParams(new { label = "label1" })
                    .DeleteAsync()
                    .ReceiveJson<AddressBookEntryModel>();

                // Assert.
                addressBook = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook")
                    .SetQueryParams(new { label = "label1" })
                    .GetJsonAsync<AddressBookModel>();

                addressBook.Addresses.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetAnAddressBook()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                // Add a few addresses.
                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label1", address = "TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label2", address = "TWMxjBk5bVdv8dhDJ645Z5RoxfrbRUJewa" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label3", address = "TC52WGLwE1KE1bXvD6f4MC7i5QtxNUGiUb" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label4", address = "TDfosbE6ChGKdH9cVpfgbKzbvFJGLs1zgq" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label5", address = "TM9i96uQDFDancRp5bUR5ea16CMWLkCYhK" })
                    .ReceiveJson<AddressBookEntryModel>();

                // Act.
                AddressBookModel addressBook = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook")
                    .GetJsonAsync<AddressBookModel>();
                
                // Assert.
                addressBook.Addresses.Should().HaveCount(5);
                addressBook.Addresses.First().Label.Should().Be("label1");
                addressBook.Addresses.First().Address.Should().Be("TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu");
                addressBook.Addresses.Last().Label.Should().Be("label5");
                addressBook.Addresses.Last().Address.Should().Be("TM9i96uQDFDancRp5bUR5ea16CMWLkCYhK");
            }
        }

        [Fact]
        public async Task GetAnAddressBookWithPagination()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Arrange.
                CoreNode node = builder.CreateStratisPosNode(this.network).Start();

                // Add a few addresses.
                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label1", address = "TQNyrEPc4qHxWN96dBAjncBeB2ghJPqYVu" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label2", address = "TWMxjBk5bVdv8dhDJ645Z5RoxfrbRUJewa" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label3", address = "TC52WGLwE1KE1bXvD6f4MC7i5QtxNUGiUb" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label4", address = "TDfosbE6ChGKdH9cVpfgbKzbvFJGLs1zgq" })
                    .ReceiveJson<AddressBookEntryModel>();

                await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook/address")
                    .PostJsonAsync(new { label = "label5", address = "TM9i96uQDFDancRp5bUR5ea16CMWLkCYhK" })
                    .ReceiveJson<AddressBookEntryModel>();

                // Act.
                AddressBookModel queryResult = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook")
                    .SetQueryParams(new { skip = 0, take = 5 })
                    .GetJsonAsync<AddressBookModel>();

                // Assert.
                queryResult.Addresses.Should().HaveCount(5);

                // Act.
                queryResult = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook")
                    .SetQueryParams(new { skip = 2, take = 3 })
                    .GetJsonAsync<AddressBookModel>();

                // Assert.
                queryResult.Addresses.Should().HaveCount(3);
                queryResult.Addresses.First().Label.Should().Be("label3");
                queryResult.Addresses.First().Address.Should().Be("TC52WGLwE1KE1bXvD6f4MC7i5QtxNUGiUb");
                queryResult.Addresses.Last().Label.Should().Be("label5");
                queryResult.Addresses.Last().Address.Should().Be("TM9i96uQDFDancRp5bUR5ea16CMWLkCYhK");

                // Act.
                queryResult = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("addressbook")
                    .SetQueryParams(new { skip = 2 })
                    .GetJsonAsync<AddressBookModel>();

                // Assert.
                queryResult.Addresses.Should().HaveCount(3);
                queryResult.Addresses.First().Label.Should().Be("label3");
                queryResult.Addresses.First().Address.Should().Be("TC52WGLwE1KE1bXvD6f4MC7i5QtxNUGiUb");
                queryResult.Addresses.Last().Label.Should().Be("label5");
                queryResult.Addresses.Last().Address.Should().Be("TM9i96uQDFDancRp5bUR5ea16CMWLkCYhK");

            }
        }
    }
}

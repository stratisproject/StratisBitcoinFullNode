using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Stratis.Bitcoin.Features.Dns.Tests
{
    /// <summary>
    /// Tests for the<see cref="DnsSeedMasterFile"/> class.
    /// </summary>
    public class GivenADnsSeedMasterFile
    {
        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            var masterFile = new DnsSeedMasterFile();
            Action a = () => { masterFile.Load(null); };

            // Act and assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("stream");
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsIPAddressResourceRecord_AndIsIPv4_ThenEntryIsPopulated()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");

            var testResourceRecord = new IPAddressResourceRecord(domain, IPAddress.Parse("192.168.0.1"));
            var question = new Question(domain, RecordType.A);

            // Act.
            IList<IResourceRecord> resourceRecords = this.WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(testResourceRecord, question);

            // Assert.
            resourceRecords.Should().NotBeNull();
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveCount(1);
            ipAddressResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
            ipAddressResourceRecords[0].IPAddress.Equals(testResourceRecord.IPAddress);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsIPAddressResourceRecord_AndIsIPv6_ThenEntryIsPopulated()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");

            var testResourceRecord = new IPAddressResourceRecord(domain, IPAddress.Parse("2001:db8:85a3:0:0:8a2e:370:7334"));
            var question = new Question(domain, RecordType.AAAA);

            // Act.
            IList<IResourceRecord> resourceRecords = this.WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(testResourceRecord, question);

            // Assert.
            resourceRecords.Should().NotBeNull();
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
            ipAddressResourceRecords.Should().HaveCount(1);
            ipAddressResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
            ipAddressResourceRecords[0].IPAddress.Equals(testResourceRecord.IPAddress);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsCanonicalNameResourceRecord_ThenEntryIsPopulated()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var cNameDomain = new Domain("www.stratis.test.com");

            var testResourceRecord = new CanonicalNameResourceRecord(domain, cNameDomain);
            var question = new Question(domain, RecordType.CNAME);

            // Act.
            IList<IResourceRecord> resourceRecords = this.WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(testResourceRecord, question);

            // Assert.
            resourceRecords.Should().NotBeNull();
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<CanonicalNameResourceRecord> canonicalResourceRecords = resourceRecords.OfType<CanonicalNameResourceRecord>().ToList();
            canonicalResourceRecords.Should().HaveCount(1);
            canonicalResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
            canonicalResourceRecords[0].CanonicalDomainName.ToString().Should().Be(testResourceRecord.CanonicalDomainName.ToString());
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsMailExchangeResourceRecord_ThenEntryIsPopulated()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var exchangeDomain = new Domain("mail.stratis.test.com");
            int preference = 10;

            var testResourceRecord = new MailExchangeResourceRecord(domain, preference, exchangeDomain);

            var question = new Question(domain, RecordType.MX);

            // Act.
            IList<IResourceRecord> resourceRecords = this.WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(testResourceRecord, question);

            // Assert.
            resourceRecords.Should().NotBeNull();
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<MailExchangeResourceRecord> mailExchangeResourceRecords = resourceRecords.OfType<MailExchangeResourceRecord>().ToList();
            mailExchangeResourceRecords.Should().HaveCount(1);
            mailExchangeResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
            mailExchangeResourceRecords[0].ExchangeDomainName.ToString().Should().Be(testResourceRecord.ExchangeDomainName.ToString());
            mailExchangeResourceRecords[0].Preference.Should().Be(preference);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsNameServerResourceRecord_ThenEntryIsPopulated()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var nsDomain = new Domain("ns");

            var testResourceRecord = new NameServerResourceRecord(domain, nsDomain);

            var question = new Question(domain, RecordType.NS);

            // Act.
            IList<IResourceRecord> resourceRecords = this.WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(testResourceRecord, question);

            // Assert.
            resourceRecords.Should().NotBeNull();
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<NameServerResourceRecord> nameServerResourceRecord = resourceRecords.OfType<NameServerResourceRecord>().ToList();
            nameServerResourceRecord.Should().HaveCount(1);
            nameServerResourceRecord[0].Name.ToString().Should().Be(domain.ToString());
            nameServerResourceRecord[0].NSDomainName.ToString().Should().Be(testResourceRecord.NSDomainName.ToString());
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsPointerResourceRecord_ThenEntryIsPopulated()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var nsDomain = new Domain("pointer.stratis.test.com");

            var testResourceRecord = new PointerResourceRecord(domain, nsDomain);

            var question = new Question(domain, RecordType.PTR);

            // Act.
            IList<IResourceRecord> resourceRecords = this.WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(testResourceRecord, question);

            // Assert.
            resourceRecords.Should().NotBeNull();
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<PointerResourceRecord> pointerResourceRecord = resourceRecords.OfType<PointerResourceRecord>().ToList();
            pointerResourceRecord.Should().HaveCount(1);
            pointerResourceRecord[0].Name.ToString().Should().Be(domain.ToString());
            pointerResourceRecord[0].PointerDomainName.ToString().Should().Be(testResourceRecord.PointerDomainName.ToString());
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsStartOfAuthorityResourceRecord_ThenEntryIsPopulated()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var masterDomain = new Domain("master.test.com");
            var responsibleDomain = new Domain("responsible.test.com");
            long serialNumber = 12121212;
            var refreshInterval = new TimeSpan(1111111111);
            var retryInterval = new TimeSpan(2222222222);
            var expireInterval = new TimeSpan(3333333333);
            var minimumTimeToLive = new TimeSpan(4444444444);

            var testResourceRecord =
                new StartOfAuthorityResourceRecord(
                    domain,
                    masterDomain,
                    responsibleDomain,
                    serialNumber,
                    refreshInterval,
                    retryInterval,
                    expireInterval,
                    minimumTimeToLive);

            var question = new Question(domain, RecordType.SOA);

            // Act.
            IList<IResourceRecord> resourceRecords = this.WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(testResourceRecord, question);

            // Assert.
            resourceRecords.Should().NotBeNull();
            resourceRecords.Should().NotBeNullOrEmpty();

            IList<StartOfAuthorityResourceRecord> startOfAuthorityResourceRecord = resourceRecords.OfType<StartOfAuthorityResourceRecord>().ToList();
            startOfAuthorityResourceRecord.Should().HaveCount(1);
            startOfAuthorityResourceRecord[0].Name.ToString().Should().Be(domain.ToString());
            startOfAuthorityResourceRecord[0].MasterDomainName.ToString().Should().Be(masterDomain.ToString());
            startOfAuthorityResourceRecord[0].ResponsibleDomainName.ToString().Should().Be(responsibleDomain.ToString());
            startOfAuthorityResourceRecord[0].SerialNumber.Should().Be(serialNumber);
            startOfAuthorityResourceRecord[0].RefreshInterval.Should().Be(refreshInterval);
            startOfAuthorityResourceRecord[0].RetryInterval.Should().Be(retryInterval);
            startOfAuthorityResourceRecord[0].ExpireInterval.Should().Be(expireInterval);
            startOfAuthorityResourceRecord[0].MinimumTimeToLive.Should().Be(minimumTimeToLive);
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenLoad_AndStreamContainsEntries_ThenEntriesArePopulated()
        {
            // Arrange.
            using (var stream = new MemoryStream())
            {
                string domainName = "stratis.test.com";
                var masterFile = new DnsSeedMasterFile();

                IList<IResourceRecord> testResourceRecords = new List<IResourceRecord>()
                {
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.0.1")),
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.0.2")),
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.0.3")),
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.0.4"))
                };

                JsonSerializer serializer = this.CreateSerializer();

                using (var streamWriter = new StreamWriter(stream))
                {
                    using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                    {
                        serializer.Serialize(jsonTextWriter, testResourceRecords);

                        jsonTextWriter.Flush();
                        stream.Seek(0, SeekOrigin.Begin);

                        // Act.
                        masterFile.Load(stream);
                    }
                }

                // Assert.
                var domain = new Domain(domainName);
                var question = new Question(domain, RecordType.A);

                IList<IResourceRecord> resourceRecords = masterFile.Get(question);
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
                ipAddressResourceRecords.Should().HaveSameCount(testResourceRecords);

                foreach (IPAddressResourceRecord testResourceRecord in testResourceRecords)
                {
                    ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testResourceRecord.IPAddress)).Should().NotBeNull();
                }
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsIPAddressResourceRecord_AndIsIPv4_ThenEntryIsSaved()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");

            var testResourceRecord = new IPAddressResourceRecord(domain, IPAddress.Parse("192.168.0.1"));
            var masterFile = new DnsSeedMasterFile(new List<IResourceRecord> { testResourceRecord });

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);

                resourceRecords.Should().NotBeNull();
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
                ipAddressResourceRecords.Should().HaveCount(1);
                ipAddressResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
                ipAddressResourceRecords[0].IPAddress.Equals(testResourceRecord.IPAddress);
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsIPAddressResourceRecord_AndIsIPv6_ThenEntryIsSaved()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");

            var testResourceRecord = new IPAddressResourceRecord(domain, IPAddress.Parse("2001:db8:85a3:0:0:8a2e:370:7334"));
            var masterFile = new DnsSeedMasterFile(new List<IResourceRecord> { testResourceRecord });

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);

                resourceRecords.Should().NotBeNull();
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
                ipAddressResourceRecords.Should().HaveCount(1);
                ipAddressResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
                ipAddressResourceRecords[0].IPAddress.Equals(testResourceRecord.IPAddress);
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsCanonicalNameResourceRecord_ThenEntryIsSaved()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var cNameDomain = new Domain("www.stratis.test.com");

            var testResourceRecord = new CanonicalNameResourceRecord(domain, cNameDomain);
            var masterFile = new DnsSeedMasterFile(new List<IResourceRecord> { testResourceRecord });

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);

                resourceRecords.Should().NotBeNull();
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<CanonicalNameResourceRecord> canonicalResourceRecords = resourceRecords.OfType<CanonicalNameResourceRecord>().ToList();
                canonicalResourceRecords.Should().HaveCount(1);
                canonicalResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
                canonicalResourceRecords[0].CanonicalDomainName.ToString().Should().Be(testResourceRecord.CanonicalDomainName.ToString());
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsMailExchangeResourceRecord_ThenEntryIsSaved()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var exchangeDomain = new Domain("mail.stratis.test.com");
            int preference = 10;

            var testResourceRecord = new MailExchangeResourceRecord(domain, preference, exchangeDomain);
            var masterFile = new DnsSeedMasterFile(new List<IResourceRecord> { testResourceRecord });

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);

                resourceRecords.Should().NotBeNull();
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<MailExchangeResourceRecord> mailExchangeResourceRecords = resourceRecords.OfType<MailExchangeResourceRecord>().ToList();
                mailExchangeResourceRecords.Should().HaveCount(1);
                mailExchangeResourceRecords[0].Name.ToString().Should().Be(domain.ToString());
                mailExchangeResourceRecords[0].ExchangeDomainName.ToString().Should().Be(testResourceRecord.ExchangeDomainName.ToString());
                mailExchangeResourceRecords[0].Preference.Should().Be(preference);
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsNameServerResourceRecord_ThenEntryIsSaved()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var nsDomain = new Domain("ns");

            var testResourceRecord = new NameServerResourceRecord(domain, nsDomain);
            var masterFile = new DnsSeedMasterFile(new List<IResourceRecord> { testResourceRecord });

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);

                resourceRecords.Should().NotBeNull();
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<NameServerResourceRecord> nameServerResourceRecord = resourceRecords.OfType<NameServerResourceRecord>().ToList();
                nameServerResourceRecord.Should().HaveCount(1);
                nameServerResourceRecord[0].Name.ToString().Should().Be(domain.ToString());
                nameServerResourceRecord[0].NSDomainName.ToString().Should().Be(testResourceRecord.NSDomainName.ToString());
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsPointerResourceRecord_ThenEntryIsSaved()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var nsDomain = new Domain("pointer.stratis.test.com");

            var testResourceRecord = new PointerResourceRecord(domain, nsDomain);
            var masterFile = new DnsSeedMasterFile(new List<IResourceRecord> { testResourceRecord });

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);

                resourceRecords.Should().NotBeNull();
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<PointerResourceRecord> pointerResourceRecord = resourceRecords.OfType<PointerResourceRecord>().ToList();
                pointerResourceRecord.Should().HaveCount(1);
                pointerResourceRecord[0].Name.ToString().Should().Be(domain.ToString());
                pointerResourceRecord[0].PointerDomainName.ToString().Should().Be(testResourceRecord.PointerDomainName.ToString());
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsStartOfAuthorityResourceRecord_ThenEntryIsSaved()
        {
            // Arrange
            var domain = new Domain("stratis.test.com");
            var masterDomain = new Domain("master.test.com");
            var responsibleDomain = new Domain("responsible.test.com");
            long serialNumber = 12121212;
            var refreshInterval = new TimeSpan(1111111111);
            var retryInterval = new TimeSpan(2222222222);
            var expireInterval = new TimeSpan(3333333333);
            var minimumTimeToLive = new TimeSpan(4444444444);

            var testResourceRecord =
                new StartOfAuthorityResourceRecord(
                    domain,
                    masterDomain,
                    responsibleDomain,
                    serialNumber,
                    refreshInterval,
                    retryInterval,
                    expireInterval,
                    minimumTimeToLive);

            var masterFile = new DnsSeedMasterFile(new List<IResourceRecord> { testResourceRecord });

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);
                resourceRecords.Should().NotBeNull();
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<StartOfAuthorityResourceRecord> startOfAuthorityResourceRecord = resourceRecords.OfType<StartOfAuthorityResourceRecord>().ToList();
                startOfAuthorityResourceRecord.Should().HaveCount(1);
                startOfAuthorityResourceRecord[0].Name.ToString().Should().Be(domain.ToString());
                startOfAuthorityResourceRecord[0].MasterDomainName.ToString().Should().Be(masterDomain.ToString());
                startOfAuthorityResourceRecord[0].ResponsibleDomainName.ToString().Should().Be(responsibleDomain.ToString());
                startOfAuthorityResourceRecord[0].SerialNumber.Should().Be(serialNumber);
                startOfAuthorityResourceRecord[0].RefreshInterval.Should().Be(refreshInterval);
                startOfAuthorityResourceRecord[0].RetryInterval.Should().Be(retryInterval);
                startOfAuthorityResourceRecord[0].ExpireInterval.Should().Be(expireInterval);
                startOfAuthorityResourceRecord[0].MinimumTimeToLive.Should().Be(minimumTimeToLive);
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndMasterListContainsEntries_ThenEntriesAreSaved()
        {
            // Arrange.
            string domainName = "stratis.test.com";

            IList<IResourceRecord> testResourceRecords = new List<IResourceRecord>()
                {
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.100.1")),
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.100.2")),
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.100.3")),
                    new IPAddressResourceRecord(new Domain(domainName), IPAddress.Parse("192.168.100.4"))
                };

            var masterFile = new DnsSeedMasterFile(testResourceRecords);

            using (var stream = new MemoryStream())
            {
                // Act.
                masterFile.Save(stream);

                // Assert.                
                stream.Should().NotBeNull();
                IList<IResourceRecord> resourceRecords = this.ReadResourceRecords(stream);
                resourceRecords.Should().NotBeNullOrEmpty();

                IList<IPAddressResourceRecord> ipAddressResourceRecords = resourceRecords.OfType<IPAddressResourceRecord>().ToList();
                ipAddressResourceRecords.Should().HaveSameCount(testResourceRecords);

                foreach (IPAddressResourceRecord testResourceRecord in testResourceRecords)
                {
                    ipAddressResourceRecords.SingleOrDefault(i => i.IPAddress.Equals(testResourceRecord.IPAddress)).Should().NotBeNull();
                }
            }
        }

        [Fact]
        [Trait("DNS", "UnitTest")]
        public void WhenSave_AndStreamIsNull_ThenArgumentNullExceptionIsThrown()
        {
            // Arrange.
            var masterFile = new DnsSeedMasterFile();
            Action a = () => { masterFile.Save(null); };

            // Act and assert.
            a.Should().Throw<ArgumentNullException>().Which.Message.Should().Contain("stream");
        }

        private IList<IResourceRecord> WhenLoad_AndStreamContainsEntry_ThenEntryIsPopulated(IResourceRecord testResourceRecord, Question question)
        {
            // Arrange.
            using (var stream = new MemoryStream())
            {
                var masterFile = new DnsSeedMasterFile();

                IList<IResourceRecord> testResourceRecords = new List<IResourceRecord>()
                {
                    testResourceRecord
                };

                JsonSerializer serializer = this.CreateSerializer();

                using (var streamWriter = new StreamWriter(stream))
                {
                    using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                    {
                        serializer.Serialize(jsonTextWriter, testResourceRecords);

                        jsonTextWriter.Flush();
                        stream.Seek(0, SeekOrigin.Begin);

                        // Act.
                        masterFile.Load(stream);
                    }
                }

                // Assert.
                return masterFile.Get(question);
            }
        }

        private JsonSerializer CreateSerializer()
        {
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            settings.Converters.Add(new ResourceRecordConverter());
            settings.Formatting = Formatting.Indented;

            return JsonSerializer.Create(settings);
        }

        private IList<IResourceRecord> ReadResourceRecords(Stream stream)
        {
            IList<IResourceRecord> resourceRecords = null;
            stream.Seek(0, SeekOrigin.Begin);

            using (var textReader = new JsonTextReader(new StreamReader(stream)))
            {
                JsonSerializer serializer = this.CreateSerializer();
                resourceRecords = serializer.Deserialize<List<IResourceRecord>>(textReader);
            }
            return resourceRecords;
        }
    }
}
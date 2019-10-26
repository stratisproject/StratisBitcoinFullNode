using System;
using System.Net;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Defines a <see cref="JsonConverter"/> implementation for an <see cref="IResourceRecord"/> object.
    /// </summary>
    public class ResourceRecordConverter : JsonConverter
    {
        private const string TypeFieldName = "Type";
        private const string IPAddressFieldName = "IPAddress";
        private const string NameFieldName = "Name";
        private const string CanonicalDomainNameFieldName = "CanonicalDomainName";
        private const string ExchangeDomainNameFieldName = "ExchangeDomainName";
        private const string PreferenceFieldName = "Preference";
        private const string NSDomainNameFieldName = "NSDomainName";
        private const string PointerDomainNameFieldName = "PointerDomainName";
        private const string MasterDomainNameFieldName = "MasterDomainName";
        private const string ResponsibleDomainNameFieldName = "ResponsibleDomainName";
        private const string SerialNumberFieldName = "SerialNumber";
        private const string RefreshIntervalFieldName = "RefreshInterval";
        private const string RetryIntervalFieldName = "RetryInterval";
        private const string ExpireIntervalFieldName = "ExpireInterval";
        private const string MinimumTimeToLiveFieldName = "MinimumTimeToLive";

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">The type of object to convert.</param>
        /// <returns><c>True</c> if the object can be converted otherwise returns <c>false</c>.</returns>
        public override bool CanConvert(Type objectType)
        {
            return (typeof(IResourceRecord).IsAssignableFrom(objectType));
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="JsonReader"/> to read from.</param>
        /// <param name="objectType">The type of object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The object value.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string resourceRecordType = jObject[TypeFieldName].Value<string>();

            if (resourceRecordType == typeof(IPAddressResourceRecord).Name)
            {
                return this.ReadIPAddressResourceRecordJson(jObject);
            }
            else if (resourceRecordType == typeof(CanonicalNameResourceRecord).Name)
            {
                return this.ReadCanonicalNameResourceRecordJson(jObject);
            }
            else if (resourceRecordType == typeof(MailExchangeResourceRecord).Name)
            {
                return ReadMailExchangeResourceRecordJson(jObject);
            }
            else if (resourceRecordType == typeof(NameServerResourceRecord).Name)
            {
                return ReadNameServerResourceRecordJson(jObject);
            }
            else if (resourceRecordType == typeof(PointerResourceRecord).Name)
            {
                return this.ReadPointerResourceRecordJson(jObject);
            }
            else if (resourceRecordType == typeof(StartOfAuthorityResourceRecord).Name)
            {
                return this.ReadStartOfAuthorityResourceRecordJson(jObject, serializer);
            }
            else
            {
                throw new ArgumentOutOfRangeException(resourceRecordType);
            }
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The Newtonsoft.Json.JsonWriter to write to.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JObject jObject;

            if (value is IPAddressResourceRecord)
            {
                jObject = this.WriteIPAddressResourceRecordJson((IPAddressResourceRecord)value);
            }
            else if (value is CanonicalNameResourceRecord)
            {
                jObject = this.WriteCanonicalNameResourceRecordJson((CanonicalNameResourceRecord)value);
            }
            else if (value is MailExchangeResourceRecord)
            {
                jObject = this.WriteMailExchangeResourceRecordJson((MailExchangeResourceRecord)value);
            }
            else if (value is NameServerResourceRecord)
            {
                jObject = this.WriteNameServerResourceRecordJson((NameServerResourceRecord)value);
            }
            else if (value is PointerResourceRecord)
            {
                jObject = this.WritePointerResourceRecordJson((PointerResourceRecord)value);
            }
            else if (value is StartOfAuthorityResourceRecord)
            {
                jObject = this.WriteStartOfAuthorityResourceRecordJson((StartOfAuthorityResourceRecord)value, serializer);
            }
            else
            {
                throw new ArgumentOutOfRangeException(value.GetType().Name);
            }

            jObject.WriteTo(writer);
        }

        /// <summary>
        /// Reads a <see cref="IPAddressResourceRecord"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="IPAddressResourceRecord"/>.</returns>
        private IPAddressResourceRecord ReadIPAddressResourceRecordJson(JObject jObject)
        {
            IPAddress ipaddress = this.ReadIPAddressJson(jObject);
            Domain domain = ReadDomainJson(jObject, NameFieldName);
            return new IPAddressResourceRecord(domain, ipaddress);
        }

        /// <summary>
        /// Reads a <see cref="CanonicalNameResourceRecord"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="CanonicalNameResourceRecord"/>.</returns>
        private CanonicalNameResourceRecord ReadCanonicalNameResourceRecordJson(JObject jObject)
        {
            Domain domain = this.ReadDomainJson(jObject, NameFieldName);
            Domain cName = this.ReadDomainJson(jObject, CanonicalDomainNameFieldName);

            return new CanonicalNameResourceRecord(domain, cName);
        }

        /// <summary>
        /// Reads a <see cref="MailExchangeResourceRecord"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="MailExchangeResourceRecord"/>.</returns>
        private MailExchangeResourceRecord ReadMailExchangeResourceRecordJson(JObject jObject)
        {
            Domain domain = this.ReadDomainJson(jObject, NameFieldName);
            Domain exchangeDomain = this.ReadDomainJson(jObject, ExchangeDomainNameFieldName);
            int preference = jObject[PreferenceFieldName].Value<int>();

            return new MailExchangeResourceRecord(domain, preference, exchangeDomain);
        }

        /// <summary>
        /// Reads a <see cref="NameServerResourceRecord"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="NameServerResourceRecord"/>.</returns>
        private NameServerResourceRecord ReadNameServerResourceRecordJson(JObject jObject)
        {
            Domain domain = this.ReadDomainJson(jObject, NameFieldName);
            Domain nsDomain = this.ReadDomainJson(jObject, NSDomainNameFieldName);
            return new NameServerResourceRecord(domain, nsDomain);
        }

        /// <summary>
        /// Reads a <see cref="PointerResourceRecord"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="PointerResourceRecord"/>.</returns>
        private PointerResourceRecord ReadPointerResourceRecordJson(JObject jObject)
        {
            Domain domain = this.ReadDomainJson(jObject, NameFieldName);
            Domain pointerDomain = this.ReadDomainJson(jObject, PointerDomainNameFieldName);
            return new PointerResourceRecord(domain, pointerDomain);
        }

        /// <summary>
        /// Reads a <see cref="StartOfAuthorityResourceRecord"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="StartOfAuthorityResourceRecord"/>.</returns>
        private StartOfAuthorityResourceRecord ReadStartOfAuthorityResourceRecordJson(JObject jObject, JsonSerializer serializer)
        {
            Domain domain = this.ReadDomainJson(jObject, NameFieldName);
            Domain masterDomain = this.ReadDomainJson(jObject, MasterDomainNameFieldName);
            Domain responsibleDomain = this.ReadDomainJson(jObject, ResponsibleDomainNameFieldName);
            long serialNumber = jObject[SerialNumberFieldName].Value<long>();

            var refreshInterval = jObject[RefreshIntervalFieldName].ToObject<TimeSpan>(serializer);
            var retryInterval = jObject[RetryIntervalFieldName].ToObject<TimeSpan>(serializer);
            var expireInterval = jObject[ExpireIntervalFieldName].ToObject<TimeSpan>(serializer);
            var minimumTimeToLive = jObject[MinimumTimeToLiveFieldName].ToObject<TimeSpan>(serializer);

            return new StartOfAuthorityResourceRecord(domain, masterDomain, responsibleDomain, serialNumber, refreshInterval, retryInterval, expireInterval, minimumTimeToLive);
        }

        /// <summary>
        /// Reads a <see cref="IPAddress"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="IPAddress"/>.</returns>
        private IPAddress ReadIPAddressJson(JObject jObject)
        {
            return IPAddress.Parse(jObject[IPAddressFieldName].Value<string>());
        }

        /// <summary>
        /// Reads a <see cref="Domain"/> from JSON.
        /// </summary>
        /// <param name="jObject">The JSON object to read from.</param>
        /// <returns>The read <see cref="Domain"/>.</returns>
        private Domain ReadDomainJson(JObject jObject, string fieldName)
        {
            return new Domain(jObject[fieldName].Value<string>());
        }

        /// <summary>
        /// Writes a <see cref="IPAddressResourceRecord"/> to JSON.
        /// </summary>
        /// <param name="resourceRecord">The <see cref="IPAddressResourceRecord"/> to write.</param>
        /// <returns>The written JSON.</returns>
        private JObject WriteIPAddressResourceRecordJson(IPAddressResourceRecord resourceRecord)
        {
            return new JObject
            {
                { TypeFieldName, resourceRecord.GetType().Name },
                { IPAddressFieldName, resourceRecord.IPAddress.ToString() },
                { NameFieldName, resourceRecord.Name.ToString()}
            };
        }

        /// <summary>
        /// Writes a <see cref="CanonicalNameResourceRecord"/> to JSON.
        /// </summary>
        /// <param name="resourceRecord">The <see cref="CanonicalNameResourceRecord"/> to write.</param>
        /// <returns>The written JSON.</returns>
        private JObject WriteCanonicalNameResourceRecordJson(CanonicalNameResourceRecord resourceRecord)
        {
            return new JObject
            {
                { TypeFieldName, resourceRecord.GetType().Name },
                { NameFieldName, resourceRecord.Name.ToString() },
                { CanonicalDomainNameFieldName, resourceRecord.CanonicalDomainName.ToString()}
             };
        }

        /// <summary>
        /// Writes a <see cref="MailExchangeResourceRecord"/> to JSON.
        /// </summary>
        /// <param name="resourceRecord">The <see cref="MailExchangeResourceRecord"/> to write.</param>
        /// <returns>The written JSON.</returns>
        private JObject WriteMailExchangeResourceRecordJson(MailExchangeResourceRecord resourceRecord)
        {
            return new JObject
            {
                { TypeFieldName, resourceRecord.GetType().Name },
                { NameFieldName, resourceRecord.Name.ToString() },
                { ExchangeDomainNameFieldName, resourceRecord.ExchangeDomainName.ToString() },
                { PreferenceFieldName, resourceRecord.Preference }
             };
        }

        /// <summary>
        /// Writes a <see cref="NameServerResourceRecord"/> to JSON.
        /// </summary>
        /// <param name="resourceRecord">The <see cref="NameServerResourceRecord"/> to write.</param>
        /// <returns>The written JSON.</returns>
        private JObject WriteNameServerResourceRecordJson(NameServerResourceRecord resourceRecord)
        {
            return new JObject
            {
                { TypeFieldName, resourceRecord.GetType().Name },
                { NameFieldName, resourceRecord.Name.ToString() },
                { NSDomainNameFieldName, resourceRecord.NSDomainName.ToString() }
             };
        }

        /// <summary>
        /// Writes a <see cref="PointerResourceRecord"/> to JSON.
        /// </summary>
        /// <param name="resourceRecord">The <see cref="PointerResourceRecord"/> to write.</param>
        /// <returns>The written JSON.</returns>
        private JObject WritePointerResourceRecordJson(PointerResourceRecord resourceRecord)
        {
            return new JObject
            {
                { TypeFieldName, resourceRecord.GetType().Name },
                { NameFieldName, resourceRecord.Name.ToString() },
                { PointerDomainNameFieldName, resourceRecord.PointerDomainName.ToString() }
             };
        }

        /// <summary>
        /// Writes a <see cref="StartOfAuthorityResourceRecord"/> to JSON.
        /// </summary>
        /// <param name="resourceRecord">The <see cref="StartOfAuthorityResourceRecord"/> to write.</param>
        /// <returns>The written JSON.</returns>
        private JObject WriteStartOfAuthorityResourceRecordJson(StartOfAuthorityResourceRecord resourceRecord, JsonSerializer serializer)
        {
            return new JObject
            {
                { TypeFieldName, resourceRecord.GetType().Name },
                { NameFieldName, resourceRecord.Name.ToString() },
                { MasterDomainNameFieldName, resourceRecord.MasterDomainName.ToString() },
                { ResponsibleDomainNameFieldName, resourceRecord.ResponsibleDomainName.ToString() },
                { SerialNumberFieldName, resourceRecord.SerialNumber },
                { RefreshIntervalFieldName, JToken.FromObject(resourceRecord.RefreshInterval, serializer)},
                { RetryIntervalFieldName, JToken.FromObject(resourceRecord.RetryInterval, serializer)},
                { ExpireIntervalFieldName, JToken.FromObject(resourceRecord.ExpireInterval, serializer)},
                { MinimumTimeToLiveFieldName, JToken.FromObject(resourceRecord.MinimumTimeToLive, serializer)}
             };
        }
    }
}
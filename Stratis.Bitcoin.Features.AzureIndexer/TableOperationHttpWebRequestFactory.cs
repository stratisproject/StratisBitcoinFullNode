//-----------------------------------------------------------------------
// <copyright file="TableOperationHttpWebRequestFactory.cs" company="Microsoft">
//    Copyright 2013 Microsoft Corporation
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// -----------------------------------------------------------------------------------------

namespace Microsoft.WindowsAzure.Storage.Table.Protocol
{
	using Microsoft.Data.OData;
	using Microsoft.WindowsAzure.Storage.Core;
	using Microsoft.WindowsAzure.Storage.Core.Util;
	using Microsoft.WindowsAzure.Storage.Shared.Protocol;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Threading;

	internal static class TableOperationHttpWebRequestFactory
	{

		internal static void WriteOdataEntity(ITableEntity entity, TableOperationType operationType, OperationContext ctx, ODataWriter writer, TableRequestOptions options, bool ignoreEncryption)
		{
			ODataEntry entry = new ODataEntry()
			{
				Properties = GetPropertiesWithKeys(entity, ctx, operationType, options, ignoreEncryption),
				TypeName = "account.sometype"
			};

			entry.SetAnnotation(new SerializationTypeNameAnnotation { TypeName = null });
			writer.WriteStart(entry);
			writer.WriteEnd();
			writer.Flush();
		}

		#region TableEntity Serialization Helpers

		internal static IEnumerable<ODataProperty> GetPropertiesFromDictionary(IDictionary<string, EntityProperty> properties, TableRequestOptions options, string partitionKey, string rowKey, bool ignoreEncryption)
		{
			return properties.Select(kvp => new ODataProperty() { Name = kvp.Key, Value = kvp.Value.PropertyAsObject });
		}

		internal static IEnumerable<ODataProperty> GetPropertiesWithKeys(ITableEntity entity, OperationContext operationContext, TableOperationType operationType, TableRequestOptions options, bool ignoreEncryption)
		{
			if(operationType == TableOperationType.Insert)
			{
				if(entity.PartitionKey != null)
				{
					yield return new ODataProperty() { Name = "PartitionKey", Value = entity.PartitionKey };
				}

				if(entity.RowKey != null)
				{
					yield return new ODataProperty() { Name = "RowKey", Value = entity.RowKey };
				}
			}

			foreach(ODataProperty property in GetPropertiesFromDictionary(entity.WriteEntity(operationContext), options, entity.PartitionKey, entity.RowKey, ignoreEncryption))
			{
				yield return property;
			}
		}
		#endregion

		
		internal static string ReadAndUpdateTableEntity(ITableEntity entity, ODataEntry entry, OperationContext ctx)
		{
			entity.ETag = entry.ETag;


			Dictionary<string, EntityProperty> entityProperties = new Dictionary<string, EntityProperty>();

			foreach(ODataProperty prop in entry.Properties)
			{
				if(prop.Name == "PartitionKey")
				{
					entity.PartitionKey = (string)prop.Value;
				}
				else if(prop.Name == "RowKey")
				{
					entity.RowKey = (string)prop.Value;
				}
				else if(prop.Name == "Timestamp")
				{
					entity.Timestamp = (DateTime)prop.Value;
				}
				else
				{
					entityProperties.Add(prop.Name, EntityProperty.CreateEntityPropertyFromObject(prop.Value));
				}
			}


			entity.ReadEntity(entityProperties, ctx);



			return entry.ETag;
		}
	}
}
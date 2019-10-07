using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Stratis.SmartContracts.CLR.Serialization
{
    /// <summary>
    /// Maps a JObject of values to the parameters on a method.
    /// </summary>
    public class JObjectParameterInfoMapper
    {
        public string[] Map(JObject obj, ParameterInfo[] parameters)
        {
            var result = new List<string>();

            foreach (var parameter in parameters)
            {
                var jObParam = obj[parameter.Name];

                if (jObParam == null)
                    throw new Exception("Couldn't map all params");

                var prefix = Prefix.ForType(parameter.ParameterType);

                result.Add($"{prefix.Value}#{jObParam}");
            }

            return result.ToArray();
        }
    }
}
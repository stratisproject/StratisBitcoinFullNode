using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    /// <summary>
    /// Class that handles method parameter serialization in the <see cref="SmartContractCarrier"/>.
    /// </summary>
    public sealed class MethodParameterSerializer : IMethodParameterSerializer
    {
        /// <inheritdoc />
        public byte[] ToBytes(string rawMethodParameters)
        {
            return Encoding.UTF8.GetBytes(rawMethodParameters);
        }

        /// <inheritdoc />
        public object[] ToObjects(string parameters)
        {
            string[] split = Regex.Split(parameters, @"(?<!(?<!\\)*\\)\|").ToArray();

            var processedParameters = new List<object>();

            foreach (var parameter in split)
            {
                string[] parameterSignature = Regex.Split(parameter.Replace(@"\|", "|"), @"(?<!(?<!\\)*\\)\#").ToArray();
                parameterSignature[1] = parameterSignature[1].Replace(@"\#", "#");

                if (parameterSignature[0] == "1")
                    processedParameters.Add(bool.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "2")
                    processedParameters.Add(Convert.ToByte(parameterSignature[1]));

                else if (parameterSignature[0] == "3")
                    processedParameters.Add(parameterSignature[1]);

                else if (parameterSignature[0] == "4")
                    processedParameters.Add(parameterSignature[1]);

                else if (parameterSignature[0] == "5")
                    processedParameters.Add(Convert.ToSByte(parameterSignature[1]));

                else if (parameterSignature[0] == "6")
                    processedParameters.Add(int.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "7")
                    processedParameters.Add(parameterSignature[1]);

                else if (parameterSignature[0] == "8")
                    processedParameters.Add(uint.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "9")
                    processedParameters.Add(new uint160(parameterSignature[1]));

                else if (parameterSignature[0] == "10")
                    processedParameters.Add(ulong.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "11")
                    processedParameters.Add(new Address(parameterSignature[1]));

                else if (parameterSignature[0] == "12")
                    processedParameters.Add(long.Parse(parameterSignature[1]));

                else
                    throw new Exception(string.Format("{0} is not supported.", parameterSignature[0]));
            }

            return processedParameters.ToArray();
        }

        /// <inheritdoc />
        public string ToRaw(string[] parameters)
        {
            IEnumerable<string> escaped = this.EscapePipesAndHashes(parameters);
            return string.Join('|', escaped);
        }

        /// <summary>
        /// Escapes any pipes and hashes in the method parameters.
        /// </summary>
        private IEnumerable<string> EscapePipesAndHashes(string[] parameter)
        {
            IEnumerable<string> processedPipes = parameter.Select(pipeparam => pipeparam = pipeparam.Replace("|", @"\|"));

            IEnumerable<string> processedHashes = processedPipes.Select(hashparam =>
            {

                // This delegate splits the string by the hash character.
                // 
                // If the split array is longer than 2 then we need to 
                // reconstruct the parameter by escaping all hashes
                // after the first one.
                // 
                // Once this is done, prepend the string with the data type,
                // which is an integer representation of SmartContractCarrierDataType,
                // as well as a hash, so that it can be split again upon deserialization.
                //
                // I.e. 3#dcg#5d# will split into 3 / dcg / 5d
                // and then dcg / fd will be reconstructed to dcg\\#5d\\# and
                // 3# prepended to make 3#dcg\\#5d\\#

                string[] hashes = hashparam.Split('#');
                if (hashes.Length == 2)
                    return hashparam;

                var reconstructed = new List<string>();
                for (int i = 1; i < hashes.Length; i++)
                {
                    reconstructed.Add(hashes[i]);
                }

                var result = string.Join('#', reconstructed).Replace("#", @"\#");
                return hashes[0].Insert(hashes[0].Length, "#" + result);
            });

            return processedHashes;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NBitcoin;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.CLR.Serialization
{
    /// <summary>
    /// Class that handles method parameter serialization.
    /// </summary>
    public sealed class MethodParameterStringSerializer : IMethodParameterStringSerializer
    {
        private readonly Network network;

        public MethodParameterStringSerializer(Network network)
        {
            this.network = network;
        }

        /// <inheritdoc />
        public string Serialize(object methodParameter)
        {
            return this.SerializeObject(methodParameter);
        }

        /// <summary>
        /// Serializes an array of method parameter objects to the bytes of their string-encoded representation.
        /// </summary>
        public string Serialize(object[] methodParameters)
        {
            var sb = new List<string>();

            foreach (object obj in methodParameters)
            {
                sb.Add(this.SerializeObject(obj));
            }

            return this.EscapeAndJoin(sb.ToArray());
        }

        private string SerializeObject(object obj)
        {
            Prefix prefix = Prefix.ForObject(obj);

            string serialized = Serialize(obj, this.network);

            return string.Format("{0}#{1}", (int) prefix.DataType, serialized);
        }

        public static string Serialize(object obj, Network network)
        {
            MethodParameterDataType primitiveType = GetPrimitiveType(obj);

            // ToString works fine for all of our data types except byte arrays and addresses
            string serialized;

            switch (primitiveType)
            {
                case MethodParameterDataType.ByteArray:
                    serialized = ((byte[]) obj).ToHexString();
                    break;
                case MethodParameterDataType.Address:
                    serialized = ((Address) obj).ToUint160().ToBase58Address(network);
                    break;
                default:
                    serialized = obj.ToString();
                    break;
            }

            return serialized;
        }

        private static MethodParameterDataType GetPrimitiveType(object o)
        {
            if (o is bool)
                return MethodParameterDataType.Bool;

            if (o is byte)
                return MethodParameterDataType.Byte;

            if (o is byte[])
                return MethodParameterDataType.ByteArray;

            if (o is char)
                return MethodParameterDataType.Char;         

            if (o is string)
                return MethodParameterDataType.String;

            if (o is uint)
                return MethodParameterDataType.UInt;
            
            if (o is ulong)
                return MethodParameterDataType.ULong;

            if (o is Address)
                return MethodParameterDataType.Address;

            if (o is long)
                return MethodParameterDataType.Long;

            if (o is int)
                return MethodParameterDataType.Int;
            
            // Any other types are not supported.
            throw new MethodParameterStringSerializerException(string.Format("{0} is not supported.", o.GetType().Name));
        }

        public object[] Deserialize(string[] parameters)
        {
            return this.StringToObjects(this.EscapeAndJoin(parameters));
        }

        public object[] Deserialize(string parameters)
        {
            return this.StringToObjects(parameters);
        }

        private object[] StringToObjects(string parameters)
        {
            string[] split = Regex.Split(parameters, @"(?<!(?<!\\)*\\)\|").ToArray();

            var processedParameters = new List<object>();

            foreach (string parameter in split)
            {
                try
                {
                    string[] parameterSignature =
                        Regex.Split(parameter.Replace(@"\|", "|"), @"(?<!(?<!\\)*\\)\#").ToArray();
                    parameterSignature[1] = parameterSignature[1].Replace(@"\#", "#");

                    if (parameterSignature[0] == MethodParameterDataType.Bool.ToString("d"))
                        processedParameters.Add(bool.Parse(parameterSignature[1]));

                    else if (parameterSignature[0] == MethodParameterDataType.Byte.ToString("d"))
                        processedParameters.Add(Convert.ToByte(parameterSignature[1]));

                    else if (parameterSignature[0] == MethodParameterDataType.Char.ToString("d"))
                        processedParameters.Add(parameterSignature[1][0]);

                    else if (parameterSignature[0] == MethodParameterDataType.String.ToString("d"))
                        processedParameters.Add(parameterSignature[1]);

                    else if (parameterSignature[0] == MethodParameterDataType.UInt.ToString("d"))
                        processedParameters.Add(uint.Parse(parameterSignature[1]));

                    else if (parameterSignature[0] == MethodParameterDataType.Int.ToString("d"))
                        processedParameters.Add(int.Parse(parameterSignature[1]));

                    else if (parameterSignature[0] == MethodParameterDataType.ULong.ToString("d"))
                        processedParameters.Add(ulong.Parse(parameterSignature[1]));

                    else if (parameterSignature[0] == MethodParameterDataType.Long.ToString("d"))
                        processedParameters.Add(long.Parse(parameterSignature[1]));

                    else if (parameterSignature[0] == MethodParameterDataType.Address.ToString("d"))
                        processedParameters.Add(parameterSignature[1].ToAddress(this.network));

                    else if (parameterSignature[0] == MethodParameterDataType.ByteArray.ToString("d"))
                        processedParameters.Add(parameterSignature[1].HexToByteArray());

                    else
                        throw new MethodParameterStringSerializerException(string.Format("{0} is not supported.",
                            parameterSignature[0]));
                }
                catch (Exception e) when (e is FormatException || e is OverflowException || e is ArgumentException || e is ArgumentNullException)
                {
                    throw new MethodParameterStringSerializerException(string.Format("Error deserializing parameter {0}", parameter), e);
                }
            }


            return processedParameters.ToArray();
        }

        /// <inheritdoc />
        private string EscapeAndJoin(string[] parameters)
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
                // which is an integer representation of MethodParameterDataType,
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

                string result = string.Join('#', reconstructed).Replace("#", @"\#");
                return hashes[0].Insert(hashes[0].Length, "#" + result);
            });

            return processedHashes;
        }
    }

    public class MethodParameterStringSerializerException : Exception
    {
        public MethodParameterStringSerializerException(string message) : base(message)
        {
        }

        public MethodParameterStringSerializerException(string message, Exception innerException)
        : base(message, innerException)
        {
        }
    }
}
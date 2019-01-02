namespace Stratis.SmartContracts.CLR.Serialization
{
    public interface IMethodParameterStringSerializer
    {
        /// <summary>
        /// Serializes a single object to its string representation.
        /// </summary>
        string Serialize(object methodParameters);

        /// <summary>
        /// Serializes an array of method parameter objects to their string representation.
        /// </summary>
        string Serialize(object[] methodParameters);

        /// <summary>
        /// Deserializes an encoded array of strings to parameter objects.
        /// </summary>
        object[] Deserialize(string[] parameters);

        /// <summary>
        /// Deserializes an encoded string of parameters to parameter objects.
        /// </summary>
        object[] Deserialize(string parameters);
    }
}
namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IMethodParameterSerializer
    {
        /// <summary>
        /// Serializes an array of method parameter objects to their byte representation.
        /// </summary>
        byte[] Serialize(object[] methodParameters);

        /// <summary>
        /// Deserializes an encoded array of strings to parameter objects.
        /// </summary>
        object[] Deserialize(string[] parameters);

        /// <summary>
        /// Deserializes an encoded array of bytes to parameter objects.
        /// </summary>
        object[] Deserialize(byte[] methodParameters);
    }
}
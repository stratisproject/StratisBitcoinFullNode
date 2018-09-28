namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IMethodParameterSerializer
    {
        /// <summary>
        /// Serializes an array of method parameter objects to their string representation.
        /// </summary>
        /// <param name="methodParameters"></param>
        byte[] ToBytes(object[] methodParameters);

        object[] ToObjects(string[] parameters);

        /// <summary>
        /// Converts the input method parameters to an object array when the carrier is created or called.
        /// </summary>
        /// <param name="methodParameters">A pipe joined representation string of unescaped method parameters.</param>
        object[] ToObjects(byte[] methodParameters);
    }
}
namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IMethodParameterSerializer
    {
        /// <summary>
        /// Converts a raw method parameter string to bytes.
        /// </summary>
        /// <param name="rawMethodParameters">A pipe joined representation string of escaped method parameters.</param>
        byte[] ToBytes(string rawMethodParameters);

        /// <summary>
        /// Converts the input method parameters to an object array when the carrier is created or called.
        /// </summary>
        /// <param name="methodParameters">A pipe joined representation string of unescaped method parameters.</param>
        object[] ToObjects(string methodParameters);

        /// <summary>
        /// Converts the input method parameters to a raw string when the carrier is created or called.
        /// </summary>
        string ToRaw(string[] methodParameters);
    }
}
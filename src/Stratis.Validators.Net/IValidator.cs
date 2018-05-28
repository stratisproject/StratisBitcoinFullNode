using Mono.Cecil;

namespace Stratis.Validators.Net
{
    public interface IValidator
    {
        /// <summary>
        /// Validate all user defined methods in the contract.
        /// <para>
        /// All methods with an empty body will be ignored.
        /// </para>
        /// </summary>
        ValidationResult Validate(ModuleDefinition moduleDefinition);
    }
}
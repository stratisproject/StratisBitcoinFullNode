using System.ComponentModel.DataAnnotations;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities.ValidationAttributes
{
    /// <summary>
    /// Validation attribute to check whether the data is in the right format to represent <see cref="Money"/>.
    /// </summary>
    public class MoneyFormatAttribute : ValidationAttribute
    {
        /// <summary> A value indicating whether the data field is required. </summary>
        private readonly bool isRequired;

        /// <summary>
        /// Initializes a new instance of the <see cref="MoneyFormatAttribute"/> class.
        /// </summary>
        /// <param name="isRequired">A value indicating whether the data field is required.</param>
        public MoneyFormatAttribute(bool isRequired = true)
        {
            this.isRequired = isRequired;
        }

        /// <inheritdoc/>
        public override bool IsValid(object value)
        {
            // Check if the value is required but not provided.
            if (this.isRequired && value == null)
            {
                return false;
            }

            // Check if the value is not required and not provided.
            if (!this.isRequired && value == null)
            {
                return true;
            }

            return Money.TryParse(value.ToString(), out Money money);
        }
    }
}

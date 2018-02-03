
namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// The result that is returned from executing each loop step.
    /// </summary>
    public enum StepResult
    {
        /// <summary>Continue execution of the loop.</summary>
        Continue,

        /// <summary>Execute the next line of code in the loop.</summary>
        Next,

        /// <summary>Break out of the loop.</summary>
        Stop,
    }
}

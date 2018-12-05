namespace Stratis.SmartContracts.CLR
{
    public interface IStateProcessor
    {
        /// <summary>
        /// Applies an externally generated contract creation message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, ExternalCreateMessage message);

        /// <summary>
        /// Applies an internally generated contract creation message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, InternalCreateMessage message);

        /// <summary>
        /// Applies an internally generated contract method call message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, InternalCallMessage message);

        /// <summary>
        /// Applies an externally generated contract method call message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, ExternalCallMessage message);

        /// <summary>
        /// Applies an internally generated contract funds transfer message to the current state.
        /// </summary>
        StateTransitionResult Apply(IState state, ContractTransferMessage message);
    }
}
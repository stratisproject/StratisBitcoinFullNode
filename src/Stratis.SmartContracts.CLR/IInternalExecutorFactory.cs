namespace Stratis.SmartContracts.CLR
{
    public interface IInternalExecutorFactory
    {
        IInternalTransactionExecutor Create(RuntimeObserver.IGasMeter gasMeter, IState state);
    }
}
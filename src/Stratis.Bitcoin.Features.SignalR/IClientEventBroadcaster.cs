namespace Stratis.Bitcoin.Features.SignalR
{
    public interface IClientEventBroadcaster
    {
        void Initialise(ClientEventBroadcasterSettings broadcasterSettings);
    }
}
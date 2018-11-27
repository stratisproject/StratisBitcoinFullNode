namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlocksRequestModel
    {
        int MaxBlocksToSend { get; set; }
        int BlockHeight { get; set; }
    }
}
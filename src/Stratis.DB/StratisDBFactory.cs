namespace Stratis.DB
{
    public interface IStratisDBFactory
    {
        IStratisDB CreateDatabase(string dataFolderName, IStratisDBSerializer stratisDBSerializer, IStratisDBTrackers stratisDBTrackers = null);
    }

    public class StratisDBFactory : IStratisDBFactory
    {
        public IStratisDB CreateDatabase(string dataFolderName, IStratisDBSerializer stratisDBSerializer, IStratisDBTrackers stratisDBTrackers = null)
        {
            return new StratisDB(dataFolderName, stratisDBSerializer, stratisDBTrackers);
        }
    }
}

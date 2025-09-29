namespace Logic.Mapek
{
    public interface IMapekManager
    {
        public void StartLoop(string instanceModelFilePath, string fmuDirectory, int maxRound = -1);

        public void StopLoop();
    }
}

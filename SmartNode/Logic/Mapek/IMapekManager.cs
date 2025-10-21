namespace Logic.Mapek
{
    public interface IMapekManager
    {
        public void StartLoop(string instanceModelFilePath, string fmuDirectory, string dataDirectory, int maxRound = -1, bool simulateTwinningTarget = false);

        public void StopLoop();
    }
}

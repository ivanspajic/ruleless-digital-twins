namespace Logic.Mapek
{
    public interface IMapekManager
    {
        public void StartLoop(string instanceModelFilePath, int maxRound = -1);

        public void StopLoop();
    }
}

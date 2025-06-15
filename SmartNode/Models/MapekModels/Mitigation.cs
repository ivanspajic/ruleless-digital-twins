namespace Models.MapekModels
{
    public class Mitigation
    {
        public required OptimalCondition UnsatisfiedOptimalCondition { get; init; }

        public required List<Action> MitigationActions { get; init; }
    }
}

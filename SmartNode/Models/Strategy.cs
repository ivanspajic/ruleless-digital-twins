namespace Models
{
    public class Strategy
    {
        private readonly IReadOnlyList<Execution> _executions;

        public Strategy(List<Execution> executions)
        {
            _executions = executions;
        }

        public IReadOnlyList<Execution> Executions => _executions;
    }
}

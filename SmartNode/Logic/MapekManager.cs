using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Logic
{
    public class MapekManager : IMapekManager
    {
        private readonly ILogger<MapekManager> _logger;

        private bool _isLoopActive = false;

        public MapekManager(ILogger<MapekManager> logger)
        {
            _logger = logger;
        }

        public void StartLoop(string filePath)
        {
            _isLoopActive = true;

            RunMapekLoop(filePath);
        }

        public void StopLoop()
        {
            _isLoopActive = false;
        }

        private void RunMapekLoop(string filePath)
        {
            _logger.LogInformation("Starting the MAPE-K loop...");

            IGraph graph = InitializeGraph(filePath);
            
            // If nothing was loaded, simply return.
            if (graph.IsEmpty)
            {
                _logger.LogInformation("There is nothing in the graph.");

                return;
            }

            while (_isLoopActive)
            {
                var propertyValueMap = Monitor(graph);
                // Monitor
                // Analyze
                // Plan
                // Execute

                // this should probably include some form of sleepy time
            }
        }

        private IGraph InitializeGraph(string filePath)
        {
            var turtleParser = new TurtleParser();
            var graph = new Graph();

            _logger.LogInformation("Loading instance model file contents from {filePath}...", filePath);

            try
            {
                turtleParser.Load(graph, filePath);
            }
            catch (Exception exception)
            {    
                _logger.LogError(exception, "Exception while loading file contents: {exceptionMessage}", exception.Message);
            }

            return graph;
        }

        private IDictionary<string, object> Monitor(IGraph graph)
        {
            // query the graph and initialize the dictionary of properties
            // get the values for the properties from the corresponding sensors
            // execute the soft sensors with some properties as inputs
                // keep executing this as long as there are inputs remaining 
        }
    }
}

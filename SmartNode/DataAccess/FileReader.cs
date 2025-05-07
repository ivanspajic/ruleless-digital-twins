using Microsoft.Extensions.Logging;

namespace DataAccess
{
    public class FileReader : IFileReader
    {
        private readonly ILogger<FileReader> _logger;

        public FileReader(ILogger<FileReader> logger)
        {
            _logger = logger;
        }

        public string ReadFileContents(string filePath)
        {
            var fileContents = string.Empty;

            if (File.Exists(filePath))
            {
                _logger.LogInformation("Reading instance model file contents...");

                fileContents = File.ReadAllText(filePath);
            }
            else
                _logger.LogInformation("No found found at {filePath}.", filePath);

            return fileContents;
        }
    }
}

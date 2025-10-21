using CsvHelper;
using CsvHelper.Configuration;
using Logic.Mapek;
using Logic.Models.MapekModels;
using System.Globalization;

namespace Logic.Utilities
{
    internal static class CsvUtils
    {
        public static void WritePropertyStatesToCsv(string directoryPath, int roundNumber, PropertyCache propertyCache)
        {
            foreach (var propertyKeyValuePair in propertyCache.Properties)
            {
                var simpleName = MapekUtilities.GetSimpleName(propertyKeyValuePair.Key);
                var filePath = Path.Combine(directoryPath, simpleName + ".csv");
                CsvUtils.WritePropertyState(filePath, roundNumber, simpleName, propertyKeyValuePair.Value.Value);
            }

            foreach (var configurableParameterKeyValuePair in propertyCache.ConfigurableParameters)
            {
                var simpleName = MapekUtilities.GetSimpleName(configurableParameterKeyValuePair.Key);
                var filePath = Path.Combine(directoryPath, simpleName + ".csv");
                CsvUtils.WritePropertyState(filePath, roundNumber, simpleName, configurableParameterKeyValuePair.Value.Value);
            }
        }

        public static void WriteActuatorStatesToCsv(string directoryPath, int roundNumber, SimulationConfiguration simulationConfiguration)
        {
            var actuatorValues = new Dictionary<string, List<object>>();

            foreach (var simulationTick in simulationConfiguration.SimulationTicks)
            {
                foreach (var action in simulationTick.ActionsToExecute)
                {
                    var simpleName = MapekUtilities.GetSimpleName(action.Actuator.Name);

                    if (actuatorValues.TryGetValue(simpleName, out List<object>? value))
                    {
                        value.Add(action.NewStateValue);
                    }
                    else
                    {
                        actuatorValues.Add(simpleName, new List<object>
                        {
                            action.NewStateValue
                        });
                    }
                }
            }

            CsvWriter csvWriter;

            foreach (var keyValuePair in actuatorValues)
            {
                var filePath = Path.Combine(directoryPath, keyValuePair.Key + ".csv");

                if (!File.Exists(filePath))
                {
                    csvWriter = GetCsvWriterFromFileMode(filePath, FileMode.Create);

                    csvWriter.WriteField("RoundNumber");
                    
                    foreach (var actuatorState in keyValuePair.Value)
                    {
                        csvWriter.WriteField("SimulationTick");
                        csvWriter.WriteField(keyValuePair.Key);
                    }
                }
                else
                {
                    csvWriter = GetCsvWriterFromFileMode(filePath, FileMode.Append);
                }

                csvWriter.NextRecord();

                csvWriter.WriteField(roundNumber);

                for (var i = 0; i < keyValuePair.Value.Count; i++)
                {
                    csvWriter.WriteField(i);
                    csvWriter.WriteField(keyValuePair.Value[i]);
                }

                csvWriter.Flush();
                csvWriter.Dispose();
            }
        }

        private static void WritePropertyState(string filePath, int roundNumber, string propertyName, object propertyValue)
        {
            CsvWriter csvWriter;

            if (!File.Exists(filePath))
            {
                csvWriter = GetCsvWriterFromFileMode(filePath, FileMode.Create);

                csvWriter.WriteField("RoundNumber");
                csvWriter.WriteField(propertyName);
            }
            else
            {
                csvWriter = GetCsvWriterFromFileMode(filePath, FileMode.Append);
            }

            csvWriter.NextRecord();

            csvWriter.WriteField(roundNumber);
            csvWriter.WriteField(propertyValue);

            csvWriter.Flush();
            csvWriter.Dispose();
        }

        private static CsvWriter GetCsvWriterFromFileMode(string filePath, FileMode fileMode)
        {
            var stream = File.Open(filePath, fileMode);
            var streamWriter = new StreamWriter(stream);
            var csvHelperConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            return new CsvWriter(streamWriter, csvHelperConfiguration);
        }
    }
}
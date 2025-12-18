using CsvHelper;
using CsvHelper.Configuration;
using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
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

        public static void WriteActuatorStatesToCsv(string directoryPath, int roundNumber, SimulationPath simulationPath) {
            var actuatorConfigurableParameterValues = new Dictionary<string, List<object>>();

            foreach (var simulationTick in simulationPath.Simulations) {
                foreach (var action in simulationTick.Actions) {
                    string simpleName;
                    object newStateOrValue;
                    if (action is ActuationAction actuationAction) {
                        simpleName = MapekUtilities.GetSimpleName(actuationAction.Actuator.Name);
                        newStateOrValue = actuationAction.NewStateValue;
                    } else {
                        var reconfigurationAction = (ReconfigurationAction)action;
                        simpleName = MapekUtilities.GetSimpleName(reconfigurationAction.ConfigurableParameter.Name);
                        newStateOrValue = reconfigurationAction.NewParameterValue;
                    }

                    if (actuatorConfigurableParameterValues.TryGetValue(simpleName, out List<object>? value)) {
                        value.Add(newStateOrValue);
                    } else {
                        actuatorConfigurableParameterValues.Add(simpleName, new List<object> {
                            newStateOrValue
                        });
                    }
                }
            }

            CsvWriter csvWriter;

            foreach (var keyValuePair in actuatorConfigurableParameterValues)
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
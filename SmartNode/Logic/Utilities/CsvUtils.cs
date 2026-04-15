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
        public static void WritePropertyStatesToCsv(string directoryPath,
            int roundNumber,
            IDictionary<string, ConfigurableParameter> configurableParameterKeyValuePairs,
            IDictionary<string, Property> propertyKeyValuePairs)
        {
            foreach (var propertyKeyValuePair in propertyKeyValuePairs)
            {
                var simpleName = MapekUtilities.GetSimpleName(propertyKeyValuePair.Key);
                var filepath = Path.Combine(directoryPath, simpleName + ".csv");
                WritePropertyState(filepath, roundNumber, simpleName, propertyKeyValuePair.Value.Value);
            }

            foreach (var configurableParameterKeyValuePair in configurableParameterKeyValuePairs)
            {
                var simpleName = MapekUtilities.GetSimpleName(configurableParameterKeyValuePair.Key);
                var filepath = Path.Combine(directoryPath, simpleName + ".csv");
                WritePropertyState(filepath, roundNumber, simpleName, configurableParameterKeyValuePair.Value.Value);
            }
        }

        public static void WriteActuatorStatesToCsv(string directoryPath, int roundNumber, Simulation simulation) {
            var actuatorConfigurableParameterValues = new Dictionary<string, List<object>>();

            foreach (var action in simulation.Actions) {
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

            CsvWriter csvWriter;

            foreach (var keyValuePair in actuatorConfigurableParameterValues) {
                var filepath = Path.Combine(directoryPath, keyValuePair.Key + ".csv");

                if (!File.Exists(filepath)) {
                    csvWriter = GetCsvWriterFromFileMode(filepath, FileMode.Create);

                    csvWriter.WriteField("RoundNumber");
                    
                    foreach (var actuatorState in keyValuePair.Value) { 
                        csvWriter.WriteField(keyValuePair.Key);
                    }
                } else {
                    csvWriter = GetCsvWriterFromFileMode(filepath, FileMode.Append);
                }

                csvWriter.NextRecord();

                csvWriter.WriteField(roundNumber);

                for (var i = 0; i < keyValuePair.Value.Count; i++) {
                    csvWriter.WriteField(keyValuePair.Value[i]);
                }

                csvWriter.Flush();
                csvWriter.Dispose();
            }
        }

        public static void WritePropertyState(string filepath, int roundNumber, string propertyName, object propertyValue) {
            CsvWriter csvWriter;

            if (!File.Exists(filepath)) {
                csvWriter = GetCsvWriterFromFileMode(filepath, FileMode.Create);

                csvWriter.WriteField("RoundNumber");
                csvWriter.WriteField(propertyName);
            } else {
                csvWriter = GetCsvWriterFromFileMode(filepath, FileMode.Append);
            }

            csvWriter.NextRecord();

            csvWriter.WriteField(roundNumber);
            csvWriter.WriteField(propertyValue);

            csvWriter.Flush();
            csvWriter.Dispose();
        }

        private static CsvWriter GetCsvWriterFromFileMode(string filePath, FileMode fileMode) {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath!);
            }

            var stream = File.Open(filePath, fileMode);
            var streamWriter = new StreamWriter(stream);
            var csvHelperConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture) {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            return new CsvWriter(streamWriter, csvHelperConfiguration);
        }
    }
}
using CsvHelper;
using CsvHelper.Configuration;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Globalization;

namespace Logic.Utilities
{
    internal static class CsvUtils
    {
        public static void WritePropertyStates(string filepath,
            int roundNumber,
            IDictionary<string, Property> properties,
            IDictionary<string, ConfigurableParameter> configurableParameters)
        {
            if (!File.Exists(filepath))
            {
                WriteNewPropertyStates(filepath, roundNumber, properties, configurableParameters);
            }
            else
            {
                WriteExistingPropertyStates(filepath, roundNumber, properties, configurableParameters);
            }
        }

        public static void WriteActuatorStates(string filepath, int roundNumber, SimulationConfiguration simulationConfiguration)
        {
            if (!File.Exists(filepath))
            {
                WriteNewActuatorStates(filepath, roundNumber, simulationConfiguration);
            }
            else
            {
                WriteExistingActuatorStates(filepath, roundNumber, simulationConfiguration);
            }
        }

        private static void WriteNewPropertyStates(string filepath,
            int roundNumber,
            IDictionary<string, Property> properties,
            IDictionary<string, ConfigurableParameter> configurableParameters)
        {
            using var stream = File.Open(filepath, FileMode.Create);

            var csvHelperConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            using var streamWriter = new StreamWriter(stream);
            using var csvWriter = new CsvWriter(streamWriter, csvHelperConfiguration);

            csvWriter.WriteField("Round");

            foreach (var property in properties)
            {
                csvWriter.WriteField(property.Key);
            }

            foreach (var configurableParameter in configurableParameters)
            {
                csvWriter.WriteField(configurableParameter.Key);
            }

            csvWriter.NextRecord();

            WritePropertyValues(csvWriter, roundNumber, properties, configurableParameters);
        }

        private static void WriteExistingPropertyStates(string filepath,
            int roundNumber,
            IDictionary<string, Property> properties,
            IDictionary<string, ConfigurableParameter> configurableParameters)
        {
            using var stream = File.Open(filepath, FileMode.Append);

            var csvHelperConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            using var streamWriter = new StreamWriter(stream);
            using var csvWriter = new CsvWriter(streamWriter, csvHelperConfiguration);

            WritePropertyValues(csvWriter, roundNumber, properties, configurableParameters);
        }

        private static void WritePropertyValues(CsvWriter csvWriter,
            int roundNumber,
            IDictionary<string, Property> properties,
            IDictionary<string, ConfigurableParameter> configurableParameters)
        {
            csvWriter.WriteField(roundNumber);

            foreach (var property in properties)
            {
                csvWriter.WriteField(property.Value.Value);
            }

            foreach (var configurableParameter in configurableParameters)
            {
                csvWriter.WriteField(configurableParameter.Value.Value);
            }

            csvWriter.NextRecord();
        }

        private static void WriteNewActuatorStates(string filepath, int roundNumber, SimulationConfiguration simulationConfiguration)
        {
            using var stream = File.Open(filepath, FileMode.Create);

            var csvHelperConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            using var streamWriter = new StreamWriter(stream);
            using var csvWriter = new CsvWriter(streamWriter, csvHelperConfiguration);

            csvWriter.WriteField("Round");

            foreach (var simulationTick in simulationConfiguration.SimulationTicks)
            {
                
            }

            csvWriter.NextRecord();

            WriteActuatorValues(csvWriter, roundNumber, properties, configurableParameters);
        }

        private static void WriteExistingActuatorStates(string filepath, int roundNumber, SimulationConfiguration simulationConfiguration)
        {
            using var stream = File.Open(filepath, FileMode.Append);

            var csvHelperConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            using var streamWriter = new StreamWriter(stream);
            using var csvWriter = new CsvWriter(streamWriter, csvHelperConfiguration);

            WriteActuatorValues(csvWriter, roundNumber, properties, configurableParameters);
        }

        private static void WriteActuatorValues(CsvWriter csvWriter, int roundNumber, SimulationConfiguration simulationConfiguration)
        {

        }
    }
}
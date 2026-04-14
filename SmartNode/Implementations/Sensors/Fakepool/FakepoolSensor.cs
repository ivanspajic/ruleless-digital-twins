using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.Fakepool {

    public class FakepoolSensor : ISensor {
        private readonly Row[] _records;

        internal class Row {
            [Name("state")]
            public double State { get; set; }
            [Name("last_updated_ts")]
            public double TS { get; set; }
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public FakepoolSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
                NewLine = Environment.NewLine,
                Delimiter = "\t",
            };
            using (var reader = new StreamReader("fakepool.tsv"))
            using (var csv = new CsvReader(reader, config))
            {   
                csv.Read();
                csv.ReadHeader();
                _records = csv.GetRecords<Row>().ToArray();
            }
        }
        public async Task<object> ObservePropertyValue(params object[] inputProperties)
        {
            return 1; // XXX Adjust
        }
    }
}
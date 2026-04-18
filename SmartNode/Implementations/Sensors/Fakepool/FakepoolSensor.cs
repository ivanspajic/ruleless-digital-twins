using System.Diagnostics;
using System.Globalization;
using System.Reflection;
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

        public int _duration = 900; // TODO: Duration of a tick in the model (15 mins), should eventually come from model.
        private int _step = 900; // "Width" of one row in the CSV.

        public FakepoolSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
                NewLine = Environment.NewLine,
                Delimiter = "\t",
            };
            var rootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName;
            var path = Path.Combine(rootDirectory, "Sensors", "Fakepool", "fakepool.tsv");
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Read();
                csv.ReadHeader();
                _records = csv.GetRecords<Row>().ToArray();
            }
        }
        public async Task<object> ObservePropertyValue(params object[] inputProperties)
        {
            // TODO: find a better way to differentiate between input parameters. Consider using Properties instead.
            Debug.Assert(inputProperties.Count() == 1);
            var dataIndex = (((int)inputProperties[0]+1) * _duration) / _step ;
            return _records[dataIndex].State;
        }
    }
}
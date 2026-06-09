using Logic.FactoryInterface;
using Microsoft.Extensions.Logging;

namespace SmartNode
{
    // In-memory scheduler for optimize_schedule plans. Thread-safe.
    // Each plan fires actuate() at hour 0, 1, 2, ... 24*time_unit_seconds relative
    // to StartedAt. Default time_unit_seconds=3600 (real hours); for demos pass
    // 60 to collapse 24h into 24 minutes.
    public static class ScheduleManager
    {
        public class ScheduleInfo
        {
            public string Id { get; set; } = "";
            public string TargetUri { get; set; } = "";
            public string TargetName { get; set; } = "";
            public List<int> OnHours { get; set; } = new();
            public double TimeUnitSeconds { get; set; }
            public DateTime StartedAt { get; set; }
            public string Status { get; set; } = "running"; // running | completed | cancelled | failed
            public int CurrentHour { get; set; } = -1;
            public string? LastError { get; set; }
        }

        private static readonly Dictionary<string, ScheduleInfo> _infos = new();
        private static readonly Dictionary<string, CancellationTokenSource> _ctss = new();
        private static readonly object _lock = new();
        private static string? _dataDir;
        private const string ScheduleFile = "schedules.json";

        public static void Configure(string dataDirectory) => _dataDir = dataDirectory;

        public static void Load()
        {
            if (_dataDir is null) return;
            var path = Path.Combine(_dataDir, ScheduleFile);
            if (!File.Exists(path)) return;
            try {
                var json = File.ReadAllText(path);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<ScheduleInfo>>(json);
                if (list is null) return;
                lock (_lock) {
                    foreach (var info in list) {
                        if (info.Status == "running") info.Status = "interrupted";
                        _infos[info.Id] = info;
                    }
                }
            } catch { }
        }

        private static void Save()
        {
            if (_dataDir is null) return;
            try {
                Directory.CreateDirectory(_dataDir);
                List<ScheduleInfo> snapshot;
                lock (_lock) { snapshot = _infos.Values.ToList(); }
                var json = System.Text.Json.JsonSerializer.Serialize(snapshot,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(_dataDir, ScheduleFile), json);
            } catch { }
        }

        public static string Start(IFactory factory, ILogger logger,
                                    string targetUri, string targetName,
                                    bool[] hoursOn, double timeUnitSeconds)
        {
            if (hoursOn.Length != 24) throw new ArgumentException("hoursOn must have length 24");

            var id = Guid.NewGuid().ToString("N")[..8];
            var cts = new CancellationTokenSource();
            var info = new ScheduleInfo
            {
                Id = id,
                TargetUri = targetUri,
                TargetName = targetName,
                OnHours = Enumerable.Range(0, 24).Where(h => hoursOn[h]).ToList(),
                TimeUnitSeconds = timeUnitSeconds,
                StartedAt = DateTime.UtcNow
            };

            lock (_lock)
            {
                _infos[id] = info;
                _ctss[id] = cts;
            }
            Save();

            _ = Task.Run(async () =>
            {
                try
                {
                    var actuator = factory.GetActuatorImplementation(targetUri);
                    int lastState = -1; // force first actuation
                    for (int h = 0; h < 24; h++)
                    {
                        var target = info.StartedAt.AddSeconds(h * timeUnitSeconds);
                        var delay = target - DateTime.UtcNow;
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, cts.Token);

                        info.CurrentHour = h;
                        int newState = hoursOn[h] ? 1 : 0;
                        if (newState != lastState)
                        {
                            await actuator.Actuate(newState);
                            logger.LogInformation("[SCHEDULE {Id}] h={H} → {State} ({Target})",
                                id, h, newState == 1 ? "ON" : "OFF", targetName);
                            lastState = newState;
                        }
                    }
                    // End of plan: force OFF.
                    var endTarget = info.StartedAt.AddSeconds(24 * timeUnitSeconds);
                    var endDelay = endTarget - DateTime.UtcNow;
                    if (endDelay > TimeSpan.Zero)
                        await Task.Delay(endDelay, cts.Token);
                    if (lastState != 0)
                    {
                        await actuator.Actuate(0);
                        logger.LogInformation("[SCHEDULE {Id}] end → OFF", id);
                    }
                    info.Status = "completed";
                    Save();
                }
                catch (OperationCanceledException)
                {
                    info.Status = "cancelled";
                    Save();
                    logger.LogInformation("[SCHEDULE {Id}] cancelled at h={H}", id, info.CurrentHour);
                }
                catch (Exception ex)
                {
                    info.Status = "failed";
                    info.LastError = ex.Message;
                    Save();
                    logger.LogError(ex, "[SCHEDULE {Id}] crashed at h={H}", id, info.CurrentHour);
                }
                finally
                {
                    lock (_lock) { _ctss.Remove(id); }
                }
            });

            return id;
        }

        public static bool Cancel(string id)
        {
            CancellationTokenSource? cts;
            lock (_lock) { _ctss.TryGetValue(id, out cts); }
            if (cts == null) return false;
            cts.Cancel();
            return true;
        }

        public static List<ScheduleInfo> List()
        {
            lock (_lock) { return _infos.Values.OrderByDescending(i => i.StartedAt).ToList(); }
        }
    }
}

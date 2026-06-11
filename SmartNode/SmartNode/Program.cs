using Logic.CaseRepository;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Mapek.Proactive;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.CommandLine;
using System.Reflection;
using System.Runtime.CompilerServices;
using Fitness;
using SmartNode.Services.Simulation;
using SmartNode.Validation;

[assembly: InternalsVisibleTo("TestProject")]

namespace SmartNode
{
    internal class Program
    {
        // P4-A: process-wide HA connection store. Seeded from HA_URL / TOKEN_HA at
        // startup, then optionally overwritten in RAM by the setup wizard (POST
        // /api/ha/connection). The HA-proxy endpoints read an atomic snapshot
        // (HaConn.GetSnapshot()) so the wizard takes effect without touching each handler.
        internal static SmartNode.Services.HomeAssistant.HaConnection HaConn { get; private set; } =
            new SmartNode.Services.HomeAssistant.HaConnection(
                Environment.GetEnvironmentVariable("HA_URL") ?? "http://localhost:8123/",
                Environment.GetEnvironmentVariable("TOKEN_HA"));

        // Read the active HA base URL (trailing slash enforced by HaConnection).
        // Still used by error messages and the Nord Pool source check.
        internal static string GetHaUrl() => HaConn.Url;

        // HttpListener prefix. Default keeps the existing localhost-only bind so native/dev
        // runs are unchanged. In Docker, set SMARTNODE_HTTP_PREFIX=http://+:8080/ so the host
        // can reach the API through the published port (localhost binds the container loopback
        // only). On Windows, '+'/'*' would need a URL ACL — that is why the default stays
        // localhost so native `dotnet run` is unaffected.
        internal static string GetHttpPrefix() => GetHttpPrefix(Environment.GetEnvironmentVariable);

        internal static string GetHttpPrefix(Func<string, string?> getEnv)
        {
            var raw = getEnv("SMARTNODE_HTTP_PREFIX");
            if (string.IsNullOrWhiteSpace(raw)) return "http://localhost:8080/";
            raw = raw.Trim();
            return raw.EndsWith("/") ? raw : raw + "/";
        }

        // Operating modes:
        //   "full"          → starts the MAPE-K loop AND the HTTP/chatbox API. Default. Used for the
        //                     showcase demo where Factory.cs has the right ruleless URI → showcase
        //                     entity_id bindings.
        //   "chatbox-only"  → starts only the HTTP listener and chatbox-facing endpoints. MAPE-K is
        //                     not started, so the showcase bindings in Factory.cs are never invoked.
        //                     Use this to plug SmartNode into an arbitrary Home Assistant via
        //                     HA_URL + TOKEN_HA without MAPE-K crashing on missing entities.
        // Any other value is treated as "full" with a warning so we never silently drift to a
        // surprising mode.
        internal static string GetMode()
        {
            var raw = (Environment.GetEnvironmentVariable("SMARTNODE_MODE") ?? "full").Trim().ToLowerInvariant();
            return raw switch {
                "full" => "full",
                "chatbox-only" => "chatbox-only",
                "" => "full",
                _ => "full"  // unknown → fall back to full; logged in Main
            };
        }

        internal static bool IsTruthy(string? raw)
        {
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value)) return false;
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        // Timeout for the per-request HttpClient that proxies /api/nlu calls to Ollama.
        // Default: 120s (preserves prior behaviour). Override via OLLAMA_TIMEOUT_SECONDS
        // env var; useful on hardware where qwen2.5-coder:7b inference for the full
        // SmartNode NLU prompt regularly exceeds 120s. Bounds: [10, 1800] seconds.
        // Anything missing, malformed, or out of range falls back to the 120s default
        // silently so a typo never strands the chat UI.
        internal static TimeSpan GetOllamaTimeout()
        {
            const int defaultSeconds = 120;
            const int minSeconds = 10;
            const int maxSeconds = 1800;
            var raw = Environment.GetEnvironmentVariable("OLLAMA_TIMEOUT_SECONDS");
            if (string.IsNullOrWhiteSpace(raw)) return TimeSpan.FromSeconds(defaultSeconds);
            if (!int.TryParse(raw.Trim(), out var seconds)) return TimeSpan.FromSeconds(defaultSeconds);
            if (seconds < minSeconds || seconds > maxSeconds) return TimeSpan.FromSeconds(defaultSeconds);
            return TimeSpan.FromSeconds(seconds);
        }

        private static async Task LogNordPoolSourceStatusAsync(ILogger logger, string? environment, string priceProvider, CancellationToken ct = default)
        {
            var area = NordPoolForecastProvider.GetArea();
            var currency = NordPoolForecastProvider.GetCurrency();
            var forecastSource = NordPoolForecastProvider.ForecastSource;
            var sourceEntity = $"sensor.nord_pool_{area.ToLowerInvariant()}_current_price";

            // Source-availability checks below only make sense for the live Home Assistant
            // Nord Pool path. For replay (or any future non-live provider), there is no
            // entity to probe and TOKEN_HA is not required, so emitting "Nord Pool source
            // unavailable: TOKEN_HA is empty" misleads operators into thinking the run is
            // misconfigured.
            if (!string.Equals(priceProvider, NordPoolForecastProvider.ProviderName, StringComparison.Ordinal)) {
                logger.LogInformation(
                    "Active price provider: {priceProvider}; Nord Pool source check skipped (only runs for {liveProvider}).",
                    priceProvider, NordPoolForecastProvider.ProviderName);
                return;
            }

            logger.LogInformation(
                "Active price provider: {priceProvider}; source={forecastSource}; area={area}; currency={currency}; replay/fake providers disabled",
                priceProvider, forecastSource, area, currency);

            if (!string.Equals(environment, "homeassistant", StringComparison.OrdinalIgnoreCase)) {
                logger.LogWarning(
                    "Nord Pool source unavailable: environment={environment}; expected homeassistant. source={source} area={area} currency={currency} forecastSource={forecastSource}",
                    environment ?? "<null>", sourceEntity, area, currency, forecastSource);
                return;
            }

            var token = Environment.GetEnvironmentVariable("TOKEN_HA") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token)) {
                logger.LogWarning(
                    "Nord Pool source unavailable: TOKEN_HA is empty. source={source} area={area} currency={currency} forecastSource={forecastSource}",
                    sourceEntity, area, currency, forecastSource);
                return;
            }

            try {
                using var http = new HttpClient { BaseAddress = new Uri(GetHaUrl()), Timeout = TimeSpan.FromSeconds(5) };
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                using var response = await http.GetAsync($"api/states/{sourceEntity}", ct);

                if (!response.IsSuccessStatusCode) {
                    logger.LogWarning(
                        "Nord Pool source unavailable: {source} returned HTTP {status}. area={area} currency={currency} forecastSource={forecastSource}",
                        sourceEntity, (int)response.StatusCode, area, currency, forecastSource);
                    return;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var state = doc.RootElement.TryGetProperty("state", out var stateEl)
                    ? stateEl.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(state) ||
                    string.Equals(state, "unknown", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(state, "unavailable", StringComparison.OrdinalIgnoreCase)) {
                    logger.LogWarning(
                        "Nord Pool source unavailable: {source} state is {state}. area={area} currency={currency} forecastSource={forecastSource}",
                        sourceEntity, state ?? "<missing>", area, currency, forecastSource);
                    return;
                }

                logger.LogInformation(
                    "Nord Pool source: {source} area={area} currency={currency} forecastSource={forecastSource}",
                    sourceEntity, area, currency, forecastSource);
            } catch (Exception ex) {
                logger.LogWarning(
                    "Nord Pool source unavailable: {source} check failed ({message}). area={area} currency={currency} forecastSource={forecastSource}",
                    sourceEntity, ex.Message, area, currency, forecastSource);
            }
        }

        static async Task<int> Main(string[] args)
        {
            RootCommand rootCommand = new();
            Option<string> fileNameArg = new("--appsettings")
            {
                Description = "Which appsettings file to use."
            };
            rootCommand.Add(fileNameArg);
            Option<string> baseDirName = new("--basedir")
            {
                Description = "The base directory for models etc. Used as prefix for all relative paths in `appsettings`."
            };
            rootCommand.Add(baseDirName);
            // ZeroOrOne arity lets `--validate-model` parse with no following path so the handler
            // can fall back to $HA_BINDINGS_FILE. With the default ExactlyOne arity, the bare
            // `--validate-model` form would fail at parse time before we ever reach the fallback.
            Option<string?> validateModelArg = new("--validate-model")
            {
                Description = "Validate the given HA bindings JSON file (or $HA_BINDINGS_FILE if no path is provided) and exit. " +
                              "Exit code 0 = PASS/WARN, 1 = FAIL, 2 = bad invocation.",
                Arity = ArgumentArity.ZeroOrOne
            };
            rootCommand.Add(validateModelArg);

            ParseResult parseResult = rootCommand.Parse(args);
            string? settingsFile = parseResult.GetValue(fileNameArg);
            string? baseDir = parseResult.GetValue(baseDirName);

            // --validate-model short-circuits the host completely: this is a static, offline check
            // (no HA reachability, no MAPE-K, no HTTP listener), so it must run before we touch
            // appsettings.json or the DI container. Returning here keeps the rest of Main untouched
            // when the flag is absent, preserving the existing demo behaviour.
            bool validateModelRequested = parseResult.Tokens.Any(t => t.Value == "--validate-model");
            if (validateModelRequested)
            {
                // Only read the option value once we know the flag is present, so a missing flag
                // never silently picks up a default and triggers validation.
                string? validateBindingsPath = parseResult.GetValue(validateModelArg);
                var pathToValidate = !string.IsNullOrWhiteSpace(validateBindingsPath)
                    ? validateBindingsPath
                    : Environment.GetEnvironmentVariable("HA_BINDINGS_FILE");
                if (string.IsNullOrWhiteSpace(pathToValidate))
                {
                    Console.Error.WriteLine("--validate-model requires either an explicit path or the HA_BINDINGS_FILE environment variable (currently unset or blank).");
                    return 2;
                }
                var report = BindingsValidator.Validate(pathToValidate);
                report.PrintTo(Console.Out);
                return report.HasFailures ? 1 : 0;
            }

            // Resolve appsettings.json relative to the assembly so `dotnet run --project ...`
            // works from any working directory.
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var appSettings = Path.Combine(assemblyDir, "Properties", settingsFile ?? "appsettings.json");

            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddJsonFile(appSettings);

            var filepathArguments = builder.Configuration.GetSection("FilepathArguments").Get<FilepathArguments>();
            var coordinatorSettings = builder.Configuration.GetSection("CoordinatorSettings").Get<CoordinatorSettings>();
            var databaseSettings = builder.Configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>();

            string priceProvider;
            try {
                priceProvider = NordPoolForecastProvider.GetConfiguredProvider();
            } catch (InvalidOperationException ex) {
                Console.Error.WriteLine("Invalid price provider configuration: " + ex.Message);
                return 2;
            }

            string? rootDirectory;
            try {
                var location = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
                // Dispatch between binary release (e.g. in Docker) and in-IDE/workspace.
                rootDirectory = baseDir ?? location!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            } catch (NullReferenceException) {
                rootDirectory = ""; // Not the most elegant solution, but it'll do.
            }

            // TODO: we can use reflection for this.
            // Fix full paths.
            filepathArguments!.OntologyFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.OntologyFilepath));
            filepathArguments.FmuDirectory = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.FmuDirectory));
            filepathArguments.DataDirectory = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.DataDirectory));
            filepathArguments.InferenceRulesFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InferenceRulesFilepath));
            filepathArguments.InstanceModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InstanceModelFilepath));
            filepathArguments.InferredModelFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InferredModelFilepath));
            filepathArguments.InferenceEngineFilepath = Path.GetFullPath(Path.Combine(rootDirectory, filepathArguments.InferenceEngineFilepath));

            // Register services here.
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");
            });
            builder.Services.AddSingleton(filepathArguments);
            builder.Services.AddSingleton(coordinatorSettings!);
            builder.Services.AddSingleton(databaseSettings!);
            // Register a factory to allow for dynamic constructor argument passing through DI.
            builder.Services.AddSingleton<IMongoClient, MongoClient>(serviceProvider => new MongoClient(databaseSettings!.ConnectionString));
            builder.Services.AddSingleton<ICaseRepository, CaseRepository>(serviceProvider => new CaseRepository(serviceProvider));
            builder.Services.AddSingleton<IFactory, Factory>(serviceProvider => new Factory(coordinatorSettings!.Environment));
            builder.Services.AddSingleton<IMapekMonitor, MapekMonitor>(serviceProvider => new MapekMonitor(serviceProvider));
            builder.Services.AddSingleton<IMapekPlan, MapekPlan>(serviceProvider => {
                MapekPlan plan = coordinatorSettings!.UseEuclid ? new EuclidMapekPlan(serviceProvider) : new MapekPlan(serviceProvider);
                
                var fitnessConfig = builder.Configuration.GetSection("FitnessSettings");
                var calculateFitness = fitnessConfig.GetValue<bool>("CalculateFitness");
                if (calculateFitness) {
                    var energyProp = fitnessConfig.GetValue<string>("EnergyProperty")
                        ?? throw new InvalidOperationException("FitnessSettings:EnergyProperty is required when CalculateFitness is true.");
                    var priceProp = fitnessConfig.GetValue<string>("PriceProperty")
                        ?? throw new InvalidOperationException("FitnessSettings:PriceProperty is required when CalculateFitness is true.");
                    var accProp = fitnessConfig.GetValue<string>("AccumulatedProperty")
                        ?? throw new InvalidOperationException("FitnessSettings:AccumulatedProperty is required when CalculateFitness is true.");
                    var tempPropStr = "http://www.semanticweb.org/rayan/ontologies/2025/ha/OfficeTemperature";

                    // 1. Base Cost (Energy * Price)
                    var f_energy = new FProp(energyProp);
                    var f_price = new FProp(priceProp);
                    var f_prod = new FBinOpArith(f_energy, f_price, (x, y) => (double)x * (double)y, name: accProp.Replace("Accumulated", ""));
                    var f_acc_cost = new FAcc<double>(f_prod, name: accProp + "_BaseCost");
                    
                    // 2. Penalty for deviating from target temperature
                    var f_temp = new FProp(tempPropStr);
                    var f_penalty = new FTargetPenalty(f_temp.Prop, name: "TemperaturePenalty");
                    var f_acc_penalty = new FAcc<double>(f_penalty, name: "AccumulatedPenalty");

                    // 3. Total Fitness = Cost + Penalty (named accProp so MapekPlan optimization finds it)
                    var f_total = new FBinOpArith(f_acc_cost, f_acc_penalty, (x, y) => (double)x + (double)y, name: accProp);

                    plan.FitnessOps = [ f_total ];
                }
                
                return plan;
            });
            builder.Services.AddSingleton<IBangBangPlanner, BangBangPlanner>(serviceProvider => new BangBangPlanner(serviceProvider));
            builder.Services.AddSingleton<IMapekExecute, MapekExecute>(serviceProvider => new MapekExecute(serviceProvider));
            builder.Services.AddSingleton<IMapekKnowledge, MapekKnowledge>(serviceProvider => new MapekKnowledge(serviceProvider));
            builder.Services.AddSingleton<IMapekManager, MapekManager>(serviceprovider => new MapekManager(serviceprovider));
            builder.Services.AddSingleton<HomeAssistantRegistry>();
            // Proactive arm (V1, consultative): MAPE-K reads the selected price provider each cycle
            // and exposes a peak/cheap-window advisory. The advisor never mutates planning state.
            builder.Services.AddSingleton<IPriceForecastProvider>(serviceProvider => priceProvider switch {
                NordPoolForecastProvider.ProviderName => new NordPoolPriceForecastAdapter(
                    serviceProvider.GetRequiredService<ILogger<NordPoolPriceForecastAdapter>>()),
                ReplayPriceForecastProvider.ProviderName => new ReplayPriceForecastProvider(
                    serviceProvider.GetRequiredService<ILogger<ReplayPriceForecastProvider>>()),
                _ => throw new InvalidOperationException($"Unsupported {NordPoolForecastProvider.ProviderEnvVar}: {priceProvider}")
            });
            builder.Services.AddSingleton<IProactiveAdvisor, ProactiveAdvisor>();
            builder.Services.AddSingleton<IFutureSimulator, SimpleFutureSimulator>();
            // MAPE-K monitor abstraction for /api/mapek/tick (issues #51, #52, #53).
            // The monitor pulls a Home Assistant state snapshot through IHaStateReader
            // and the active price source through IPriceForecastProvider (registered
            // above). Both deps are optional ctor params: in modes where one is not
            // wired, the tick degrades to an explicit "unavailable" warning instead
            // of throwing or inventing data.
            builder.Services.AddSingleton<SmartNode.Mapek.Monitoring.IHaStateReader,
                SmartNode.Mapek.Monitoring.HaStateReader>();
            builder.Services.AddSingleton<SmartNode.Mapek.Monitoring.IMapekMonitorService,
                SmartNode.Mapek.Monitoring.MapekMonitorService>();
            // Minimal Analyzer phase (issue #59) — pure function over the
            // Monitor's RuntimeState plus the active goals and the (currently
            // empty) selected plan. No HA call, no mutation.
            builder.Services.AddSingleton<SmartNode.Mapek.Analysis.IMapekAnalyzerService,
                SmartNode.Mapek.Analysis.MapekAnalyzerService>();
            // Decision log provider. Singleton so every
            // /api/mapek/tick appends to the same store that /api/mapek/decisions
            // reads. Default = in-memory (no runtime file; offline demo unchanged).
            // DECISION_LOG_PROVIDER=sqlite gives a durable log that survives restarts.
            // Fail-fast: an unknown provider throws here; a bad sqlite path throws
            // on the eager resolve below — never a silent fallback to memory.
            var decisionLogOptions = SmartNode.Services.Decisions.DecisionLogOptions.Parse(Environment.GetEnvironmentVariable);
            if (decisionLogOptions.Provider == SmartNode.Services.Decisions.DecisionLogOptions.ProviderSqlite)
            {
                builder.Services.AddSingleton<SmartNode.Services.Decisions.IDecisionLog>(
                    _ => new SmartNode.Services.Decisions.SqliteDecisionLog(decisionLogOptions.SqlitePath));
            }
            else
            {
                builder.Services.AddSingleton<SmartNode.Services.Decisions.IDecisionLog,
                    SmartNode.Services.Decisions.InMemoryDecisionLog>();
            }

            // P7-B / P7-B2 — cross-tick execution history backing cooldown/rate-limit.
            // Default = SQLite so the safety window survives restart. Explicitly set
            // MAPEK_EXECUTION_HISTORY_PROVIDER=memory for transient test/dev runs.
            // Fail-fast on an unknown provider or a bad sqlite path (no silent fallback).
            var execHistoryOptions = SmartNode.Services.Execution.ExecutionHistoryOptions.Parse(Environment.GetEnvironmentVariable);
            if (execHistoryOptions.Provider == SmartNode.Services.Execution.ExecutionHistoryOptions.ProviderSqlite)
            {
                builder.Services.AddSingleton<SmartNode.Services.Execution.IExecutionHistory>(
                    _ => new SmartNode.Services.Execution.SqliteExecutionHistory(execHistoryOptions.SqlitePath));
            }
            else
            {
                builder.Services.AddSingleton<SmartNode.Services.Execution.IExecutionHistory,
                    SmartNode.Services.Execution.InMemoryExecutionHistory>();
            }

            // P1 — safety-event audit log (full trail of blocked/failed/executed
            // real-execution decisions). Default = memory (no new DB file, existing
            // behaviour unchanged); MAPEK_SAFETY_EVENT_LOG_PROVIDER=sqlite gives a
            // durable audit trail. Fail-fast on an unknown provider or bad path.
            var safetyEventOptions = SmartNode.Services.Safety.SafetyEventLogOptions.Parse(Environment.GetEnvironmentVariable);
            if (safetyEventOptions.Provider == SmartNode.Services.Safety.SafetyEventLogOptions.ProviderSqlite)
            {
                builder.Services.AddSingleton<SmartNode.Services.Safety.ISafetyEventLog>(
                    _ => new SmartNode.Services.Safety.SqliteSafetyEventLog(safetyEventOptions.SqlitePath));
            }
            else
            {
                builder.Services.AddSingleton<SmartNode.Services.Safety.ISafetyEventLog,
                    SmartNode.Services.Safety.InMemorySafetyEventLog>();
            }

            // Goal repository provider. Default = json (current
            // behaviour, hot-reloaded each tick). GOAL_REPOSITORY_PROVIDER=sqlite gives
            // a durable, DB-backed repository. Fail-fast on an unknown provider here and
            // on a bad sqlite path via the eager resolve below — no silent fallback.
            var goalRepoOptions = SmartNode.Services.Goals.GoalRepositoryOptions.Parse(Environment.GetEnvironmentVariable);
            if (goalRepoOptions.Provider == SmartNode.Services.Goals.GoalRepositoryOptions.ProviderSqlite)
            {
                builder.Services.AddSingleton<SmartNode.Services.Goals.IGoalRepository>(sp =>
                    new SmartNode.Services.Goals.SqliteGoalRepository(
                        goalRepoOptions.SqlitePath,
                        goalRepoOptions.SeedFromJsonPath,
                        sp.GetService<ILogger<SmartNode.Services.Goals.GoalRepository>>()));
            }
            else
            {
                builder.Services.AddSingleton<SmartNode.Services.Goals.IGoalRepository>(sp =>
                    new SmartNode.Services.Goals.JsonGoalRepository(
                        goalRepoOptions.JsonPath,
                        sp.GetService<ILogger<SmartNode.Services.Goals.GoalRepository>>()));
            }

            // Non-secret product settings store. Default = memory; SQLite is opt-in for
            // durable settings via SETTINGS_STORE_PROVIDER=sqlite. This is intentionally
            // not wired into safety/runtime controls: it persists UI/product settings only
            // and rejects credential-like keys or values.
            var settingsStoreOptions = SmartNode.Services.Settings.SettingsStoreOptions.Parse(Environment.GetEnvironmentVariable);
            if (settingsStoreOptions.Provider == SmartNode.Services.Settings.SettingsStoreOptions.ProviderSqlite)
            {
                builder.Services.AddSingleton<SmartNode.Services.Settings.ISettingsStore>(
                    _ => new SmartNode.Services.Settings.SqliteSettingsStore(settingsStoreOptions.SqlitePath));
            }
            else
            {
                builder.Services.AddSingleton<SmartNode.Services.Settings.ISettingsStore,
                    SmartNode.Services.Settings.InMemorySettingsStore>();
            }

            using var host = builder.Build();

            // Fail-fast at startup: force storage singletons to construct now so a bad sqlite
            // configuration is reported immediately, not at the first tick. With the
            // execution history defaulting to SQLite, a non-writable path must fail
            // closed at boot rather than silently disable cooldown/rate-limit later.
            _ = host.Services.GetRequiredService<SmartNode.Services.Decisions.IDecisionLog>();
            _ = host.Services.GetRequiredService<SmartNode.Services.Goals.IGoalRepository>();
            _ = host.Services.GetRequiredService<SmartNode.Services.Execution.IExecutionHistory>();
            _ = host.Services.GetRequiredService<SmartNode.Services.Safety.ISafetyEventLog>();
            _ = host.Services.GetRequiredService<SmartNode.Services.Settings.ISettingsStore>();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Decision log provider: {provider}{path}.",
                decisionLogOptions.Provider,
                decisionLogOptions.Provider == SmartNode.Services.Decisions.DecisionLogOptions.ProviderSqlite
                    ? $" ({decisionLogOptions.SqlitePath})" : string.Empty);
            logger.LogInformation("Goal repository provider: {provider}{path}.",
                goalRepoOptions.Provider,
                goalRepoOptions.Provider == SmartNode.Services.Goals.GoalRepositoryOptions.ProviderSqlite
                    ? $" ({goalRepoOptions.SqlitePath})" : $" ({goalRepoOptions.JsonPath})");
            logger.LogInformation("Execution history provider: {provider}{path}.",
                execHistoryOptions.Provider,
                execHistoryOptions.Provider == SmartNode.Services.Execution.ExecutionHistoryOptions.ProviderSqlite
                    ? $" ({execHistoryOptions.SqlitePath})" : string.Empty);
            logger.LogInformation("Safety event log provider: {provider}{path}.",
                safetyEventOptions.Provider,
                safetyEventOptions.Provider == SmartNode.Services.Safety.SafetyEventLogOptions.ProviderSqlite
                    ? $" ({safetyEventOptions.SqlitePath})" : string.Empty);
            logger.LogInformation("Settings store provider: {provider}{path}.",
                settingsStoreOptions.Provider,
                settingsStoreOptions.Provider == SmartNode.Services.Settings.SettingsStoreOptions.ProviderSqlite
                    ? $" ({settingsStoreOptions.SqlitePath})" : string.Empty);
            await LogNordPoolSourceStatusAsync(logger, coordinatorSettings!.Environment, priceProvider);

            // MAPE-K manager resolution is deferred to the full-mode branch below.
            // Resolving it here would force-construct the whole DI chain
            // (MapekManager -> MapekMonitor -> MapekKnowledge), which loads
            // models-and-rules/homeassistant-ha-inferred.ttl in its constructor.
            // That file is gitignored and only produced by the standalone inference
            // engine, so eager construction crashes a fresh chatbox-only start that
            // does not actually need MAPE-K.
            var haRegistry = host.Services.GetRequiredService<HomeAssistantRegistry>();
            haRegistry.Start();

            // Restore schedule history from previous run.
            ScheduleManager.Configure(filepathArguments.DataDirectory);
            ScheduleManager.Load();

            // Start internal API
            _ = Task.Run(async () => {
                try {
                    var listener = new System.Net.HttpListener();
                    var httpPrefix = GetHttpPrefix();
                    listener.Prefixes.Add(httpPrefix);
                    listener.Start();
                    logger.LogInformation("Internal API listening on {prefix}", httpPrefix);
                    while (true) {
                        var context = await listener.GetContextAsync();
                        // Dispatch each request on a background task so a slow handler
                        // (e.g. Ollama on first load) cannot block subsequent requests.
                        _ = Task.Run(async () => {
                        try {
                        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                        if (context.Request.HttpMethod == "OPTIONS") {
                            context.Response.StatusCode = 200;
                            context.Response.Close();
                            return;
                        }

                        if (context.Request.Url!.AbsolutePath == "/api/health") {
                            if (context.Request.HttpMethod != "GET") {
                                context.Response.StatusCode = 405;
                                context.Response.ContentType = "application/json";
                                var methodBytes = System.Text.Encoding.UTF8.GetBytes(
                                    System.Text.Json.JsonSerializer.Serialize(new { error = "Method not allowed. Use GET /api/health." }));
                                context.Response.OutputStream.Write(methodBytes, 0, methodBytes.Length);
                                context.Response.Close();
                                return;
                            }

                            var result = SmartNode.Services.Health.HealthEndpoint.Health();
                            context.Response.StatusCode = result.StatusCode;
                            context.Response.ContentType = "application/json";
                            var json = System.Text.Json.JsonSerializer.Serialize(result.Payload);
                            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            context.Response.Close();
                            return;
                        } else if (context.Request.Url!.AbsolutePath == "/api/ready") {
                            if (context.Request.HttpMethod != "GET") {
                                context.Response.StatusCode = 405;
                                context.Response.ContentType = "application/json";
                                var methodBytes = System.Text.Encoding.UTF8.GetBytes(
                                    System.Text.Json.JsonSerializer.Serialize(new { error = "Method not allowed. Use GET /api/ready." }));
                                context.Response.OutputStream.Write(methodBytes, 0, methodBytes.Length);
                                context.Response.Close();
                                return;
                            }

                            var result = SmartNode.Services.Health.HealthEndpoint.Ready(
                                goalRepoOptions,
                                decisionLogOptions,
                                execHistoryOptions,
                                settingsStoreOptions,
                                host.Services.GetRequiredService<SmartNode.Services.Goals.IGoalRepository>(),
                                host.Services.GetRequiredService<SmartNode.Services.Decisions.IDecisionLog>(),
                                host.Services.GetRequiredService<SmartNode.Services.Execution.IExecutionHistory>(),
                                host.Services.GetRequiredService<SmartNode.Services.Settings.ISettingsStore>());
                            context.Response.StatusCode = result.StatusCode;
                            context.Response.ContentType = "application/json";
                            var json = System.Text.Json.JsonSerializer.Serialize(result.Payload);
                            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            context.Response.Close();
                            return;
                        } else if (context.Request.Url!.AbsolutePath == "/api/price") {
                            try {
                                var env = coordinatorSettings!.Environment;
                                string? haJson = null;
                                // Strict mode: in env=homeassistant we never fall through to the legacy
                                // factory-sensor path, because that path can surface Fakepool data and
                                // the HA demo must only serve real live prices (or an explicit refusal).
                                const string HaRefusalWarning =
                                    "Live Home Assistant Nord Pool data unavailable; refusing to use fallback prices.";

                                // WP3: explicit replay branch. Active only when PRICE_PROVIDER=replay was
                                // selected at startup. Reads PRICE_REPLAY_FILE; never contacts Home
                                // Assistant; never reads TOKEN_HA. On a missing/invalid file it returns
                                // HTTP 503 with a clean envelope — no silent fallback to HA or fake.
                                if (string.Equals(priceProvider, ReplayPriceForecastProvider.ProviderName, StringComparison.Ordinal)) {
                                    var replayProvider = host.Services.GetRequiredService<IPriceForecastProvider>();
                                    try {
                                        var replayForecast = await replayProvider.GetForecastAsync();
                                        var replaySlots = replayForecast.Slots.Select(s => new {
                                            start = s.Start.ToString("o"),
                                            end = s.End.ToString("o"),
                                            price = Math.Round(s.Price, 4)
                                        }).ToArray();
                                        var replayPrices = replayForecast.Slots.Select(s => Math.Round(s.Price, 4)).ToArray();
                                        // `current` should reflect the slot active at wall-clock now, not just
                                        // the first slot in the file (which may be in the past or future depending
                                        // on the replay recording). Fall back to the first price only if no slot
                                        // covers `now`, so the UI stays compatible with empty-current cases.
                                        var nowUtc = DateTimeOffset.UtcNow;
                                        var currentSlot = replayForecast.Slots
                                            .FirstOrDefault(s => s.Start <= nowUtc && nowUtc < s.End);
                                        var replayCurrent = currentSlot is not null
                                            ? Math.Round(currentSlot.Price, 4)
                                            : (replayPrices.Length > 0 ? replayPrices[0] : 0d);
                                        var replayJson = System.Text.Json.JsonSerializer.Serialize(new {
                                            priceProvider,
                                            source = string.IsNullOrWhiteSpace(replayForecast.Source)
                                                ? ReplayPriceForecastProvider.ForecastSource : replayForecast.Source,
                                            area = replayForecast.Area,
                                            currency = replayForecast.Currency,
                                            unit = replayForecast.Unit,
                                            timezone = replayForecast.Timezone,
                                            forecastAvailable = true,
                                            forecast = new {
                                                forecastAvailable = true,
                                                forecastSource = string.IsNullOrWhiteSpace(replayForecast.Source)
                                                    ? ReplayPriceForecastProvider.ForecastSource : replayForecast.Source,
                                                area = replayForecast.Area,
                                                currency = replayForecast.Currency,
                                                timezone = replayForecast.Timezone,
                                                slots = replaySlots
                                            },
                                            current = replayCurrent,
                                            prices = replayPrices,
                                            replayFile = ReplayPriceForecastProvider.GetResolvedFile()
                                        });
                                        var replayBytes = System.Text.Encoding.UTF8.GetBytes(replayJson);
                                        context.Response.StatusCode = 200;
                                        context.Response.ContentType = "application/json";
                                        context.Response.OutputStream.Write(replayBytes, 0, replayBytes.Length);
                                        context.Response.Close();
                                        return;
                                    } catch (ReplayPriceForecastProvider.ReplayLoadException rex) {
                                        logger.LogWarning(
                                            "/api/price: replay provider failed ({msg}); refusing silent fallback to HA or fake prices",
                                            rex.Message);
                                        var refusal = System.Text.Json.JsonSerializer.Serialize(new {
                                            priceProvider,
                                            forecastAvailable = false,
                                            warning = rex.Message,
                                            source = ReplayPriceForecastProvider.ForecastSource,
                                            replayFile = ReplayPriceForecastProvider.GetResolvedFile(),
                                            prices = Array.Empty<double>()
                                        });
                                        var refusalBytes = System.Text.Encoding.UTF8.GetBytes(refusal);
                                        context.Response.StatusCode = 503;
                                        context.Response.ContentType = "application/json";
                                        context.Response.OutputStream.Write(refusalBytes, 0, refusalBytes.Length);
                                        context.Response.Close();
                                        return;
                                    }
                                }

                                // env=homeassistant: try the live Nord Pool aggregate sensors. If they
                                // (or nordpool.get_prices_for_date) are unreachable we DO NOT fall back
                                // to the legacy factory sensor — instead we refuse with HTTP 503 below.
                                if (env == "homeassistant") {
                                    try {
                                        var token = Environment.GetEnvironmentVariable("TOKEN_HA") ?? string.Empty;
                                        var area = NordPoolForecastProvider.GetArea().ToLowerInvariant();
                                        using var http = new HttpClient { BaseAddress = new Uri(NordPoolForecastProvider.GetHaUrl()), Timeout = TimeSpan.FromSeconds(5) };
                                        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                                        string prefix = $"sensor.nord_pool_{area}_";
                                        string[] names = new[] {
                                            "current_price", "next_price", "previous_price",
                                            "lowest_price", "highest_price", "daily_average",
                                            "peak_average", "off_peak_1_average", "off_peak_2_average"
                                        };
                                        var responses = await Task.WhenAll(names.Select(n => http.GetAsync($"api/states/{prefix}{n}")));
                                        if (responses.All(r => r.IsSuccessStatusCode)) {
                                            var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
                                            var byName = new Dictionary<string, string>();
                                            for (int i = 0; i < names.Length; i++) byName[names[i]] = bodies[i];

                                            double GetState(string n) {
                                                using var d = System.Text.Json.JsonDocument.Parse(byName[n]);
                                                var s = d.RootElement.GetProperty("state").GetString() ?? "0";
                                                return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                                            }
                                            string GetUnit(string n) {
                                                using var d = System.Text.Json.JsonDocument.Parse(byName[n]);
                                                if (d.RootElement.TryGetProperty("attributes", out var a) &&
                                                    a.TryGetProperty("unit_of_measurement", out var u)) return u.GetString() ?? "";
                                                return "";
                                            }
                                            async Task<string> GetOptionalTimestampState(string n) {
                                                var entityId = $"{prefix}{n}";
                                                try {
                                                    using var response = await http.GetAsync($"api/states/{entityId}");
                                                    if (!response.IsSuccessStatusCode) {
                                                        logger.LogWarning("/api/price: Nord Pool timestamp sensor {entityId} returned HTTP {status}; leaving value empty",
                                                            entityId, (int)response.StatusCode);
                                                        return "";
                                                    }

                                                    var body = await response.Content.ReadAsStringAsync();
                                                    using var d = System.Text.Json.JsonDocument.Parse(body);
                                                    var raw = d.RootElement.TryGetProperty("state", out var s)
                                                        ? (s.GetString() ?? "").Trim()
                                                        : "";

                                                    if (string.IsNullOrWhiteSpace(raw) ||
                                                        string.Equals(raw, "unknown", StringComparison.OrdinalIgnoreCase) ||
                                                        string.Equals(raw, "unavailable", StringComparison.OrdinalIgnoreCase)) {
                                                        logger.LogWarning("/api/price: Nord Pool timestamp sensor {entityId} state is {state}; leaving value empty",
                                                            entityId, string.IsNullOrWhiteSpace(raw) ? "<empty>" : raw);
                                                        return "";
                                                    }

                                                    if (DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                                                        System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)) {
                                                        return parsed.ToString("o");
                                                    }

                                                    logger.LogWarning("/api/price: Nord Pool timestamp sensor {entityId} has invalid state {state}; leaving value empty",
                                                        entityId, raw);
                                                    return "";
                                                } catch (Exception ex) {
                                                    logger.LogWarning("/api/price: Nord Pool timestamp sensor {entityId} fetch failed ({message}); leaving value empty",
                                                        entityId, ex.Message);
                                                    return "";
                                                }
                                            }

                                            var current = GetState("current_price");
                                            var op1FromTask = GetOptionalTimestampState("off_peak_1_time_from");
                                            var op1UntilTask = GetOptionalTimestampState("off_peak_1_time_until");
                                            var op2FromTask = GetOptionalTimestampState("off_peak_2_time_from");
                                            var op2UntilTask = GetOptionalTimestampState("off_peak_2_time_until");
                                            await Task.WhenAll(op1FromTask, op1UntilTask, op2FromTask, op2UntilTask);
                                            var op1 = (from: op1FromTask.Result, until: op1UntilTask.Result);
                                            var op2 = (from: op2FromTask.Result, until: op2UntilTask.Result);

                                            // Real future forecast via nordpool.get_prices_for_date (auto-discovered config_entry).
                                            var forecast = await NordPoolForecastProvider.GetForecastAsync(token, logger);
                                            object forecastBlock;
                                            double[] prices;
                                            string? warning = null;

                                            if (forecast.ForecastAvailable) {
                                                prices = forecast.Slots.Select(s => Math.Round(s.Price, 4)).ToArray();
                                                forecastBlock = new {
                                                    forecastAvailable = true,
                                                    forecastSource = forecast.Source,
                                                    configEntryDiscovered = forecast.ConfigEntryDiscovered,
                                                    area = forecast.Area,
                                                    currency = forecast.Currency,
                                                    timezone = forecast.Timezone,
                                                    slots = forecast.Slots.Select(s => new {
                                                        start = s.Start.ToString("o"),
                                                        end = s.End.ToString("o"),
                                                        hourLocal = s.HourLocal,
                                                        price = Math.Round(s.Price, 4)
                                                    }).ToArray()
                                                };
                                            } else {
                                                prices = new[] { current };
                                                warning = forecast.Warning ?? "Nord Pool future price forecast unavailable";
                                                forecastBlock = new {
                                                    forecastAvailable = false,
                                                    forecastSource = forecast.Source,
                                                    configEntryDiscovered = forecast.ConfigEntryDiscovered,
                                                    area = forecast.Area,
                                                    currency = forecast.Currency,
                                                    timezone = forecast.Timezone,
                                                    warning,
                                                    slots = Array.Empty<object>()
                                                };
                                            }

                                            haJson = System.Text.Json.JsonSerializer.Serialize(new {
                                                priceProvider,
                                                source = "homeassistant:nordpool",
                                                current,
                                                next = GetState("next_price"),
                                                previous = GetState("previous_price"),
                                                lowest = GetState("lowest_price"),
                                                highest = GetState("highest_price"),
                                                dailyAverage = GetState("daily_average"),
                                                peakAverage = GetState("peak_average"),
                                                offPeak1Average = GetState("off_peak_1_average"),
                                                offPeak2Average = GetState("off_peak_2_average"),
                                                offPeak1 = new { from = op1.from, until = op1.until },
                                                offPeak2 = new { from = op2.from, until = op2.until },
                                                unit = GetUnit("current_price"),
                                                forecast = forecastBlock,
                                                forecastAvailable = forecast.ForecastAvailable,
                                                warning,
                                                prices
                                            });
                                        } else {
                                            logger.LogWarning("/api/price: Nord Pool fetch returned non-success ({codes}); refusing to use Fakepool/legacy fallback in homeassistant mode",
                                                string.Join(",", responses.Select(r => (int)r.StatusCode)));
                                        }
                                    } catch (Exception npEx) {
                                        logger.LogWarning("/api/price: Nord Pool fetch failed ({msg}); refusing to use Fakepool/legacy fallback in homeassistant mode", npEx.Message);
                                    }

                                    // Strict refusal: in homeassistant mode we never serve simulated
                                    // prices. Emit a clean JSON envelope with HTTP 503 and bail out
                                    // before the legacy factory path runs.
                                    if (haJson == null) {
                                        logger.LogWarning("/api/price: refusing fake/legacy price fallback in homeassistant mode; returning 503 with priceProvider={priceProvider}", priceProvider);
                                        var refusal = System.Text.Json.JsonSerializer.Serialize(new {
                                            priceProvider,
                                            forecastAvailable = false,
                                            warning = HaRefusalWarning,
                                            source = "homeassistant:nordpool",
                                            prices = Array.Empty<double>()
                                        });
                                        var refusalBytes = System.Text.Encoding.UTF8.GetBytes(refusal);
                                        context.Response.StatusCode = 503;
                                        context.Response.ContentType = "application/json";
                                        context.Response.OutputStream.Write(refusalBytes, 0, refusalBytes.Length);
                                        context.Response.Close();
                                        return;
                                    }
                                }

                                // Legacy/fallback path: read the factory-registered price sensor 24 times.
                                // Reached only for non-homeassistant environments (e.g. roomM370) — the
                                // HA branch above always returns before getting here.
                                if (haJson == null) {
                                    var factory = host.Services.GetRequiredService<IFactory>();
                                    var priceSensorUri = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceSensor";
                                    var priceProcUri = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceProcedure";
                                    var sensor = factory.GetSensorImplementation(priceSensorUri, priceProcUri);

                                    var prices = new System.Collections.Generic.List<double>();
                                    for(int i=0; i<24; i++) {
                                        var p = await sensor.ObservePropertyValue(i);
                                        prices.Add(Convert.ToDouble(p));
                                    }
                                    haJson = System.Text.Json.JsonSerializer.Serialize(new { prices });
                                }

                                var bytes = System.Text.Encoding.UTF8.GetBytes(haJson);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/entities") {
                            try {
                                var factory = host.Services.GetRequiredService<IFactory>();
                                var sensors = factory.ListSensorKeys()
                                    .Select(k => new { uri = k.SensorName, procedure = k.ProcedureName })
                                    .ToList();
                                var actuators = factory.ListActuatorKeys().ToList();
                                var json = System.Text.Json.JsonSerializer.Serialize(new { sensors, actuators });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/state") {
                            try {
                                var factory = host.Services.GetRequiredService<IFactory>();
                                var readings = new System.Collections.Generic.List<object>();
                                foreach (var (sensorUri, procUri) in factory.ListSensorKeys()) {
                                    try {
                                        var sensor = factory.GetSensorImplementation(sensorUri, procUri);
                                        // Pass 0 as default timestep (FakepoolSensor needs it, HomeAssistantSensor ignores it).
                                        var value = await sensor.ObservePropertyValue(0);
                                        readings.Add(new { uri = sensorUri, procedure = procUri, value = value?.ToString() ?? "null", ok = true });
                                    } catch (Exception e) {
                                        readings.Add(new { uri = sensorUri, procedure = procUri, value = (string?)null, ok = false, error = e.Message });
                                    }
                                }
                                var json = System.Text.Json.JsonSerializer.Serialize(new { readings });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/actuate" && context.Request.HttpMethod == "POST") {
                            try {
                                using var reader = new StreamReader(context.Request.InputStream);
                                var body = await reader.ReadToEndAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(body);
                                var uri = doc.RootElement.GetProperty("uri").GetString();
                                // Accept any JSON Number (int or float) — InputNumber needs full precision.
                                var state = doc.RootElement.GetProperty("state").GetDouble();
                                if (uri == null) {
                                    context.Response.StatusCode = 400;
                                } else {
                                    var factory = host.Services.GetRequiredService<IFactory>();
                                    var actuator = factory.GetActuatorImplementation(uri);
                                    await actuator.Actuate(state);
                                    logger.LogInformation($"[CHATBOX] Actuate {uri} => {state}");
                                    var json = System.Text.Json.JsonSerializer.Serialize(new { ok = true, uri, state });
                                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                    context.Response.ContentType = "application/json";
                                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                                }
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/states") {
                            // Proxy to HA /api/states. Returns the full entity catalog so the frontend
                            // can build a dynamic resolver from any HA instance (local or remote).
                            try {
                                var creds = HaConn.GetSnapshot();
                                var token = creds.Token ?? string.Empty;
                                using var http = new HttpClient { BaseAddress = new Uri(creds.Url), Timeout = TimeSpan.FromSeconds(5) };
                                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                                var raw = await http.GetStringAsync("api/states");
                                var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 502;
                                var msg = $"Home Assistant unreachable at {GetHaUrl()}: {ex.Message}";
                                var bytes = System.Text.Encoding.UTF8.GetBytes(msg);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/services") {
                            // Proxy to HA /api/services. Lets the frontend know which services exist
                            // for each domain in the current HA instance (e.g. light.turn_on with brightness).
                            try {
                                var creds = HaConn.GetSnapshot();
                                var token = creds.Token ?? string.Empty;
                                using var http = new HttpClient { BaseAddress = new Uri(creds.Url), Timeout = TimeSpan.FromSeconds(5) };
                                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                                var raw = await http.GetStringAsync("api/services");
                                var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 502;
                                var msg = $"Home Assistant unreachable at {GetHaUrl()}: {ex.Message}";
                                var bytes = System.Text.Encoding.UTF8.GetBytes(msg);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/config") {
                            // Proxy to HA /api/config. Useful for displaying instance name/version.
                            try {
                                var creds = HaConn.GetSnapshot();
                                var token = creds.Token ?? string.Empty;
                                using var http = new HttpClient { BaseAddress = new Uri(creds.Url), Timeout = TimeSpan.FromSeconds(5) };
                                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                                var raw = await http.GetStringAsync("api/config");
                                var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 502;
                                var msg = $"Home Assistant unreachable at {GetHaUrl()}: {ex.Message}";
                                var bytes = System.Text.Encoding.UTF8.GetBytes(msg);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/call_service" && context.Request.HttpMethod == "POST") {
                            try {
                                using var reader = new StreamReader(context.Request.InputStream);
                                var body = await reader.ReadToEndAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(body);
                                var domain = doc.RootElement.GetProperty("domain").GetString();
                                var service = doc.RootElement.GetProperty("service").GetString();

                                var creds = HaConn.GetSnapshot();
                                var token = creds.Token ?? string.Empty;
                                using var http = new HttpClient { BaseAddress = new Uri(creds.Url), Timeout = TimeSpan.FromSeconds(5) };
                                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                                var reqBody = "{}";
                                if (doc.RootElement.TryGetProperty("data", out var dataProp)) {
                                    reqBody = dataProp.GetRawText();
                                }

                                var res = await http.PostAsync($"api/services/{domain}/{service}", new StringContent(reqBody, System.Text.Encoding.UTF8, "application/json"));
                                var resContent = await res.Content.ReadAsStringAsync();

                                context.Response.StatusCode = (int)res.StatusCode;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(resContent);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 502;
                                var msg = $"Home Assistant unreachable at {GetHaUrl()}: {ex.Message}";
                                var bytes = System.Text.Encoding.UTF8.GetBytes(msg);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/entities_full") {
                            try {
                                var registry = host.Services.GetRequiredService<HomeAssistantRegistry>();
                                var list = registry.GetAll();
                                var json = System.Text.Json.JsonSerializer.Serialize(list);
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/nlu" && context.Request.HttpMethod == "POST") {
                            // Proxy user message through Ollama (qwen2.5-coder) to get a structured intent.
                            try {
                                using var reader = new StreamReader(context.Request.InputStream);
                                var inBody = await reader.ReadToEndAsync();
                                using var inDoc = System.Text.Json.JsonDocument.Parse(inBody);
                                var userMessage = inDoc.RootElement.GetProperty("message").GetString() ?? "";

                                var registry = host.Services.GetRequiredService<HomeAssistantRegistry>();
                                var entitiesList = registry.SummaryForPrompt();

                                var systemPrompt = $@"You are an NLU module for a smart-home assistant. The user may write in English or French. Reply ONLY with a JSON object (no prose, no code fences) matching this schema:
{{
  ""intent"": one of [""greeting"", ""capabilities"", ""smalltalk"", ""price_current"", ""price_cheapest"", ""price_expensive"", ""price_average"", ""set_temperature"", ""call_service"", ""query_state"", ""optimize_schedule"", ""out_of_scope"", ""unknown""],
  ""domain"": null | string (for call_service, e.g. ""light"", ""scene"", ""cover"", ""climate""),
  ""service"": null | string (for call_service, e.g. ""turn_on"", ""turn_off"", ""set_temperature""),
  ""entity_id"": null | string (for call_service or query_state, must be exact entity ID),
  ""data"": null | object (for call_service, e.g. {{""temperature"": 22}}),
  ""value"": null | number (for direct orders or legacy set_temperature),
  ""duration_hours"": null | integer (required for optimize_schedule),
  ""deadline_hour"": null | integer 0-24 (required for optimize_schedule),
  ""start_hour"": null | integer 0-23 (optional, only when the user gives an explicit time window like ""between 2am and 7am""),
  ""budget_max"": null | number in NOK (optional),
  ""power_kw"": null | number in kW,
  ""target"": null | string (for optimize_schedule, e.g. ""CarCharger"" or ""HeaterActuator""),
  ""answer"": a short English answer for the user (1-2 sentences)
}}

Here are the AVAILABLE entities in the home:
{entitiesList}

Rules:
- To control ANY entity discovered above (lights, scenes, switches, media players, scripts), use intent=""call_service"". Set the correct domain, service, and entity_id. Put any arguments in data.
  * e.g. ""turn on the kitchen"" -> intent=""call_service"", domain=""light"", service=""turn_on"", entity_id=""light.showcase_kitchen_light"", data={{""entity_id"": ""light.showcase_kitchen_light""}}
  * e.g. ""turn off movie mode"" -> intent=""call_service"", domain=""input_boolean"", service=""turn_off"", entity_id=""input_boolean.showcase_movie_mode"", data={{""entity_id"": ""input_boolean.showcase_movie_mode""}}
- IMPORTANT: To turn ON or OFF a mode (like Movie Mode or Sleep Mode), ALWAYS prefer the 'input_boolean' entity rather than 'scene'. Scenes cannot be turned off.
- If the user implies a macro-action like leaving home (""I'm leaving"", ""bye"", ""I'm going to work"") or going to bed (""good night""), LOOK for a relevant 'script' (e.g. script.showcase_leave_home, script.showcase_good_night) or 'scene' and call it.
- For ""set the temperature to 21 degrees"" (DIRECT ORDER) -> use intent=""set_temperature"", value=21, OR call_service on the climate entity if available.
- PRIORITY RULE: any request to charge a car / EV / Tesla / vehicle MUST be intent=""optimize_schedule"", even when the user mentions ""cheapest"" or ""lowest price"" — those are constraints, NOT a price-query intent.
  * e.g. ""charge the Tesla to 100% by 7am at the cheapest rate"" -> intent=""optimize_schedule"", target=""CarCharger"", duration_hours=4, deadline_hour=7, power_kw=11
  * e.g. ""charge the car between 2am and 7am at the lowest price"" -> intent=""optimize_schedule"", target=""CarCharger"", duration_hours=5, start_hour=2, deadline_hour=7, power_kw=11
- For questions about current states (temperature, humidity, is a door open) -> intent=""query_state"", and set ""entity_id"" to the asked entity.
- NEVER default to ""LivingRoomLight"" or ""light.showcase_living_room_lamp"" when the user names a different entity (e.g. ""purifier"", ""kitchen"", ""hallway"", ""movie mode"", ""sleep""). If the requested entity is not in the AVAILABLE list above, return intent=""unknown"" with an answer asking the user to clarify — do NOT pick the living-room lamp as a fallback.
- For anything outside smart home / energy -> intent=""out_of_scope"".";

                                var payload = new {
                                    model = "qwen2.5-coder:7b",
                                    messages = new[] {
                                        new { role = "system", content = systemPrompt },
                                        new { role = "user", content = userMessage }
                                    },
                                    stream = false,
                                    format = "json"
                                };
                                var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
                                using var ollama = new HttpClient { Timeout = GetOllamaTimeout() };
                                var resp = await ollama.PostAsync("http://localhost:11434/api/chat",
                                    new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json"));
                                var respBody = await resp.Content.ReadAsStringAsync();
                                using var respDoc = System.Text.Json.JsonDocument.Parse(respBody);
                                var content = respDoc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "{}";

                                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/target_temp" && context.Request.HttpMethod == "POST") {
                            try {
                                using var reader = new StreamReader(context.Request.InputStream);
                                var body = await reader.ReadToEndAsync();
                                var data = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, double>>(body);

                                if (data != null && data.ContainsKey("temperature")) {
                                    FTargetPenalty.TargetValue = data["temperature"];
                                    logger.LogInformation($"[CHATBOX] Dynamic constraint received: Target Temperature set to {FTargetPenalty.TargetValue}°C");
                                    context.Response.StatusCode = 200;
                                } else {
                                    context.Response.StatusCode = 400;
                                }
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/execute_schedule" && context.Request.HttpMethod == "POST") {
                            // Register a 24-cell schedule for async actuation.
                            // Body: { target: URI, target_name: str, hours_on: bool[24], time_unit_seconds: number }
                            try {
                                using var reader = new StreamReader(context.Request.InputStream);
                                var body = await reader.ReadToEndAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(body);
                                var targetUri = doc.RootElement.GetProperty("target").GetString()!;
                                var targetName = doc.RootElement.TryGetProperty("target_name", out var tn) && tn.ValueKind == System.Text.Json.JsonValueKind.String
                                    ? tn.GetString()! : targetUri;
                                var timeUnit = doc.RootElement.TryGetProperty("time_unit_seconds", out var tu) && tu.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? tu.GetDouble() : 3600.0;
                                var hoursOnEl = doc.RootElement.GetProperty("hours_on");
                                var hoursOn = new bool[24];
                                for (int i = 0; i < 24 && i < hoursOnEl.GetArrayLength(); i++) {
                                    hoursOn[i] = hoursOnEl[i].GetBoolean();
                                }

                                var factory = host.Services.GetRequiredService<IFactory>();
                                var id = ScheduleManager.Start(factory, logger, targetUri, targetName, hoursOn, timeUnit);

                                var json = System.Text.Json.JsonSerializer.Serialize(new { ok = true, schedule_id = id, time_unit_seconds = timeUnit });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/schedules" && context.Request.HttpMethod == "GET") {
                            try {
                                var list = ScheduleManager.List().Select(s => new {
                                    id = s.Id,
                                    target = s.TargetName,
                                    target_uri = s.TargetUri,
                                    status = s.Status,
                                    on_hours = s.OnHours,
                                    current_hour = s.CurrentHour,
                                    time_unit_seconds = s.TimeUnitSeconds,
                                    started_at = s.StartedAt.ToString("o"),
                                    last_error = s.LastError
                                }).ToList();
                                var json = System.Text.Json.JsonSerializer.Serialize(new { schedules = list });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/cancel_schedule" && context.Request.HttpMethod == "POST") {
                            try {
                                using var reader = new StreamReader(context.Request.InputStream);
                                var body = await reader.ReadToEndAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(body);
                                var id = doc.RootElement.GetProperty("id").GetString()!;
                                var ok = ScheduleManager.Cancel(id);
                                var json = System.Text.Json.JsonSerializer.Serialize(new { ok, id });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/optimize" && context.Request.HttpMethod == "POST") {
                            // This is the first production path toward MAPE-K future-cost optimization:
                            // price forecast comes from Nord Pool (auto-discovered config_entry, real
                            // hourly prices from nordpool.get_prices_for_date), EV consumption forecast
                            // is simulated from candidate schedules. Later, general consumption should
                            // come from the digital twin/FMUs for each simulation path.
                            try {
                                using var reader = new StreamReader(context.Request.InputStream);
                                var body = await reader.ReadToEndAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(body);

                                // Nord Pool slots can be sub-hourly (typically 15 min). duration_hours is a
                                // *duration* in hours requested by the user, not a slot count.
                                double requestedDurationHours = doc.RootElement.TryGetProperty("duration_hours", out var durEl) && durEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? durEl.GetDouble() : 4.0;
                                int deadlineHour = doc.RootElement.TryGetProperty("deadline_hour", out var dhEl) && dhEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? dhEl.GetInt32() : 24;
                                int? startHour = doc.RootElement.TryGetProperty("start_hour", out var shEl) && shEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? shEl.GetInt32() : (int?)null;
                                double? budget = doc.RootElement.TryGetProperty("budget_max", out var bmEl) && bmEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? bmEl.GetDouble() : (double?)null;
                                double powerKw = doc.RootElement.TryGetProperty("power_kw", out var pkEl) && pkEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? pkEl.GetDouble() : 1.0;
                                string? target = doc.RootElement.TryGetProperty("target", out var tgEl) && tgEl.ValueKind == System.Text.Json.JsonValueKind.String
                                    ? tgEl.GetString() : null;

                                var token = Environment.GetEnvironmentVariable("TOKEN_HA") ?? string.Empty;
                                var forecast = await NordPoolForecastProvider.GetForecastAsync(token, logger);

                                if (!forecast.ForecastAvailable) {
                                    var noFc = System.Text.Json.JsonSerializer.Serialize(new {
                                        ok = false,
                                        optimized = false,
                                        forecastAvailable = false,
                                        forecast_available = false,
                                        error = "Live Nord Pool forecast unavailable from Home Assistant",
                                        reason = forecast.Warning ?? "Future Nord Pool price forecast unavailable",
                                        warning = forecast.Warning,
                                        price_source = "homeassistant_nordpool",
                                        priceSource = forecast.Source,
                                        configEntryDiscovered = forecast.ConfigEntryDiscovered,
                                        area = forecast.Area,
                                        currency = forecast.Currency
                                    });
                                    var noFcBytes = System.Text.Encoding.UTF8.GetBytes(noFc);
                                    context.Response.ContentType = "application/json";
                                    context.Response.OutputStream.Write(noFcBytes, 0, noFcBytes.Length);
                                    logger.LogWarning("[CHATBOX] Optimize aborted: forecast unavailable ({reason})", forecast.Warning);
                                    return;
                                }

                                // Build candidate window aligned to wall-clock local time, using the same
                                // timezone the forecast was projected into.
                                TimeZoneInfo tz;
                                try { tz = TimeZoneInfo.FindSystemTimeZoneById(forecast.Timezone); }
                                catch { try { tz = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); } catch { tz = TimeZoneInfo.Utc; } }
                                var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

                                // Resolve deadline as the next wall-clock occurrence of `deadline_hour` in the future.
                                int dl = Math.Clamp(deadlineHour, 1, 48);
                                var deadlineCandidate = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, nowLocal.Offset).AddHours(dl);
                                if (deadlineCandidate <= nowLocal) deadlineCandidate = deadlineCandidate.AddDays(1);

                                DateTimeOffset windowStart = nowLocal;
                                if (startHour is int sh) {
                                    int sHour = Math.Clamp(sh, 0, 23);
                                    var startCandidate = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, nowLocal.Offset).AddHours(sHour);
                                    if (startCandidate <= nowLocal) startCandidate = startCandidate.AddDays(1);
                                    if (startCandidate >= deadlineCandidate) startCandidate = startCandidate.AddDays(-1);
                                    windowStart = startCandidate > nowLocal ? startCandidate : nowLocal;
                                }

                                // Aggregate sub-hourly Nord Pool slots (typically 15 min) into full-hour buckets.
                                // /api/optimize works in whole hours so it stays compatible with /api/execute_schedule
                                // (24 hourly cells) and the demo time mode (1h = 1min). /api/price still exposes the
                                // raw 15-min slots for callers that want full resolution.
                                static double SlotHours(NordPoolForecastProvider.Slot s) => Math.Max(0, (s.End - s.Start).TotalHours);

                                var allBuckets = forecast.Slots
                                    .GroupBy(s => new DateTimeOffset(s.Start.Year, s.Start.Month, s.Start.Day, s.Start.Hour, 0, 0, s.Start.Offset))
                                    .Select(g => {
                                        var bucketStart = g.Key;
                                        var bucketEnd = bucketStart.AddHours(1);
                                        var slots = g.OrderBy(s => s.Start).ToList();
                                        double covered = slots.Sum(SlotHours);
                                        double avgPrice = covered > 0
                                            ? slots.Sum(s => s.Price * SlotHours(s)) / covered
                                            : slots.Average(s => s.Price);
                                        return new {
                                            Start = bucketStart,
                                            End = bucketEnd,
                                            AvgPrice = avgPrice,
                                            Complete = covered >= 0.999  // tolerate float jitter; 4×0.25 = 1.0
                                        };
                                    })
                                    .ToList();

                                // Keep only complete future hours fully contained in [windowStart, deadlineCandidate].
                                var hourBuckets = allBuckets
                                    .Where(b => b.Complete && b.Start >= windowStart && b.End <= deadlineCandidate)
                                    .OrderBy(b => b.Start)
                                    .ToList();

                                var cheapest3 = string.Join(", ",
                                    hourBuckets.OrderBy(b => b.AvgPrice).Take(3)
                                        .Select(b => $"{b.Start:HH:mm}@{b.AvgPrice:F4}"));
                                logger.LogInformation(
                                    "[OPTIMIZE] priceSource=homeassistant_nordpool target={tg} powerKw={pk} duration={dh}h rawSlots={fs} hourlyBuckets={ab} windowBuckets={hb} windowStart={ws:HH:mm} deadline={dc:HH:mm} | cheapest3={c}",
                                    target ?? "<none>", powerKw, requestedDurationHours,
                                    forecast.Slots.Count, allBuckets.Count, hourBuckets.Count,
                                    windowStart, deadlineCandidate, cheapest3);

                                if (hourBuckets.Count == 0) {
                                    var emptyJson = System.Text.Json.JsonSerializer.Serialize(new {
                                        optimized = false,
                                        forecastAvailable = true,
                                        reason = "No complete future hours within the requested window",
                                        priceSource = forecast.Source,
                                        windowStart = windowStart.ToString("o"),
                                        deadline = deadlineCandidate.ToString("o")
                                    });
                                    var eb = System.Text.Encoding.UTF8.GetBytes(emptyJson);
                                    context.Response.ContentType = "application/json";
                                    context.Response.OutputStream.Write(eb, 0, eb.Length);
                                    return;
                                }

                                // Each bucket is 1h, so #buckets = #hours. duration_hours is rounded to the nearest
                                // whole hour and capped by what the window allows.
                                int needed = Math.Max(1, (int)Math.Round(requestedDurationHours, MidpointRounding.AwayFromZero));
                                needed = Math.Min(needed, hourBuckets.Count);

                                var chosenBuckets = hourBuckets
                                    .OrderBy(b => b.AvgPrice)
                                    .ThenBy(b => b.Start)
                                    .Take(needed)
                                    .OrderBy(b => b.Start)
                                    .ToList();

                                double actualDurationHours = needed; // 1h per bucket
                                double totalCost   = chosenBuckets.Sum(b => b.AvgPrice * powerKw); // ×1h
                                double avgWindow   = hourBuckets.Average(b => b.AvgPrice);
                                // Window-average baseline: charging the same energy at the window's mean hourly price.
                                // Conservative — represents "you didn't optimize within the deadline window".
                                double baselineCost = avgWindow * powerKw * requestedDurationHours;
                                double savingsPct  = baselineCost > 0 ? (1 - totalCost / baselineCost) * 100 : 0;
                                double avgChosen   = chosenBuckets.Average(b => b.AvgPrice);

                                // Worst-N baseline: same energy charged at the N most expensive hours available
                                // within the deadline window. This is the upper bound of achievable savings —
                                // i.e. "the worst plan you could have picked under the same constraint".
                                var worstBuckets = hourBuckets
                                    .OrderByDescending(b => b.AvgPrice)
                                    .ThenBy(b => b.Start)
                                    .Take(needed)
                                    .ToList();
                                double worstAvgPrice = worstBuckets.Count > 0 ? worstBuckets.Average(b => b.AvgPrice) : avgWindow;
                                double worstCost = worstAvgPrice * powerKw * requestedDurationHours;
                                double worstSavingsPct = worstCost > 0 ? (1 - totalCost / worstCost) * 100 : 0;

                                var chosenStarts = new HashSet<DateTimeOffset>(chosenBuckets.Select(b => b.Start));

                                // 24-cell schedule keyed on local hour-of-day. on=true iff the bucket for that
                                // hour was chosen — guarantees schedule[].on matches chosen_slots exactly so the
                                // Run-plan executor never runs more hours than were optimized.
                                var schedule = new System.Collections.Generic.List<object>(24);
                                var bucketsByHour = hourBuckets
                                    .GroupBy(b => b.Start.Hour)
                                    .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Start).First());
                                for (int h = 0; h < 24; h++) {
                                    if (bucketsByHour.TryGetValue(h, out var b)) {
                                        schedule.Add(new {
                                            hour = h,
                                            price = Math.Round(b.AvgPrice, 4),
                                            on = chosenStarts.Contains(b.Start),
                                            before_deadline = b.End <= deadlineCandidate,
                                            in_window = b.Start >= windowStart && b.End <= deadlineCandidate
                                        });
                                    } else {
                                        schedule.Add(new {
                                            hour = h, price = (double?)null, on = false,
                                            before_deadline = false, in_window = false
                                        });
                                    }
                                }

                                var chosenSlotsOut = chosenBuckets.Select(b => new {
                                    start = b.Start.ToString("o"),
                                    end = b.End.ToString("o"),
                                    hour = b.Start.Hour,
                                    duration_hours = 1.0,
                                    price = Math.Round(b.AvgPrice, 4),
                                    cost = Math.Round(b.AvgPrice * powerKw, 4)
                                }).ToList();

                                var result = new {
                                    optimized = true,
                                    forecastAvailable = true,
                                    forecast_available = true,
                                    price_source = "homeassistant_nordpool",
                                    priceSource = forecast.Source,
                                    raw_slot_count = forecast.Slots.Count,
                                    hourly_bucket_count = hourBuckets.Count,
                                    configEntryDiscovered = forecast.ConfigEntryDiscovered,
                                    area = forecast.Area,
                                    currency = forecast.Currency,
                                    timezone = forecast.Timezone,
                                    target,
                                    chosen_slots = chosenSlotsOut,
                                    chosen_hours = chosenBuckets.Select(b => b.Start.Hour).OrderBy(h => h).ToList(),
                                    requested_duration_hours = Math.Round(requestedDurationHours, 4),
                                    duration_hours = (int)Math.Round(actualDurationHours),
                                    actual_duration_hours = Math.Round(actualDurationHours, 4),
                                    slot_count = chosenBuckets.Count,
                                    start_hour = windowStart.Hour,
                                    deadline_hour = deadlineCandidate.Hour == 0 ? 24 : deadlineCandidate.Hour,
                                    power_kw = powerKw,
                                    total_cost_nok = Math.Round(totalCost, 2),
                                    baseline_cost_nok = Math.Round(baselineCost, 2),
                                    baseline_worst_cost_nok = Math.Round(worstCost, 2),
                                    baseline_worst_avg_price = Math.Round(worstAvgPrice, 4),
                                    baseline_worst_savings_percent = (int)Math.Round(worstSavingsPct),
                                    avg_price = Math.Round(avgWindow, 4),
                                    avg_price_chosen = Math.Round(avgChosen, 4),
                                    savings_percent = (int)Math.Round(savingsPct),
                                    budget_max = budget,
                                    within_budget = budget == null || totalCost <= budget.Value,
                                    schedule
                                };

                                var json = System.Text.Json.JsonSerializer.Serialize(result);
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                context.Response.ContentType = "application/json";
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                                logger.LogInformation($"[CHATBOX] Optimize: target={target}, requested={requestedDurationHours:F2}h actual={actualDurationHours:F2}h ({chosenBuckets.Count} hourly buckets) in [{windowStart:HH:mm}..{deadlineCandidate:HH:mm}), cost={totalCost:F2} {forecast.Currency} ({savingsPct:F0}% saved)");
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/proactive/status" && context.Request.HttpMethod == "GET") {
                            // Read-only view of the proactive arm: returns the latest advisory computed
                            // by MAPE-K (cheap/peak window detection from the Nord Pool forecast).
                            // Returns 204 if MAPE-K hasn't produced an advisory yet.
                            try {
                                var advisor = host.Services.GetRequiredService<IProactiveAdvisor>();
                                var latest = advisor.Latest;
                                if (latest is null) {
                                    context.Response.StatusCode = 204;
                                } else {
                                    var json = System.Text.Json.JsonSerializer.Serialize(new {
                                        forecastAvailable = latest.ForecastAvailable,
                                        warning = latest.Warning,
                                        generatedAt = latest.GeneratedAt.ToString("o"),
                                        currency = latest.Currency,
                                        area = latest.Area,
                                        currentPrice = latest.CurrentPrice,
                                        q1 = latest.Q1,
                                        q3 = latest.Q3,
                                        nextPeakStart = latest.NextPeakStart?.ToString("o"),
                                        nextPeakPrice = latest.NextPeakPrice,
                                        hoursUntilNextPeak = latest.HoursUntilNextPeak,
                                        shouldPreheat = latest.ShouldPreheat,
                                        shouldDeferLoad = latest.ShouldDeferLoad,
                                        reason = latest.Reason
                                    });
                                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                    context.Response.ContentType = "application/json";
                                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                                }
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/model/validation" && context.Request.HttpMethod == "GET") {
                            // Default: WP2 V1 offline bindings validation. Explicit `?live=true`
                            // adds WP2 V2 Home Assistant reachability checks after the same
                            // offline validator has run. The bindings path comes only from
                            // HA_BINDINGS_FILE; there is intentionally no client-controlled path.
                            try {
                                var liveRequested = string.Equals(
                                    context.Request.QueryString["live"],
                                    "true",
                                    StringComparison.OrdinalIgnoreCase);
                                var bindingsPath = Environment.GetEnvironmentVariable("HA_BINDINGS_FILE");
                                if (string.IsNullOrWhiteSpace(bindingsPath)) {
                                    context.Response.StatusCode = 400;
                                    context.Response.ContentType = "application/json";
                                    var errJson = System.Text.Json.JsonSerializer.Serialize(new {
                                        mode = liveRequested ? "live" : "offline",
                                        error = "HA_BINDINGS_FILE is not set; nothing to validate.",
                                        checksLiveHomeAssistant = liveRequested
                                    });
                                    var errBytes = System.Text.Encoding.UTF8.GetBytes(errJson);
                                    context.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
                                } else {
                                    var report = SmartNode.Validation.BindingsValidator.Validate(bindingsPath);
                                    SmartNode.Validation.LiveHomeAssistantValidationSummary? liveSummary = null;
                                    if (liveRequested) {
                                        using var liveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                        liveSummary = await SmartNode.Validation.LiveHomeAssistantValidator.ValidateAsync(
                                            report,
                                            bindingsPath,
                                            GetHaUrl(),
                                            Environment.GetEnvironmentVariable("TOKEN_HA"),
                                            liveCts.Token);
                                    }
                                    // PASS or WARN → 200; FAIL → 422 so a client can distinguish a
                                    // structurally broken binding from a server crash (500).
                                    context.Response.StatusCode = report.HasFailures ? 422 : 200;
                                    context.Response.ContentType = "application/json";
                                    var errors = report.Issues
                                        .Where(i => i.Severity == SmartNode.Validation.ValidationSeverity.Error)
                                        .Select(i => new { code = i.Code, message = i.Message })
                                        .ToArray();
                                    var warnings = report.Issues
                                        .Where(i => i.Severity == SmartNode.Validation.ValidationSeverity.Warning)
                                        .Select(i => new { code = i.Code, message = i.Message })
                                        .ToArray();
                                    var json = liveRequested && liveSummary != null
                                        ? System.Text.Json.JsonSerializer.Serialize(new {
                                            mode = "live",
                                            source = report.SourcePath,
                                            profile = report.Profile,
                                            result = report.Status,
                                            errorCount = report.ErrorCount,
                                            warningCount = report.WarningCount,
                                            sensors = report.SensorCount,
                                            actuators = report.ActuatorCount,
                                            errors,
                                            warnings,
                                            checksLiveHomeAssistant = true,
                                            haUrl = liveSummary.HaUrl,
                                            haEntitiesChecked = liveSummary.HaEntitiesChecked,
                                            haEntitiesReachable = liveSummary.HaEntitiesReachable,
                                            haServicesChecked = liveSummary.HaServicesChecked,
                                            haServicesReachable = liveSummary.HaServicesReachable
                                        })
                                        : System.Text.Json.JsonSerializer.Serialize(new {
                                            mode = "offline",
                                            source = report.SourcePath,
                                            profile = report.Profile,
                                            result = report.Status,
                                            errorCount = report.ErrorCount,
                                            warningCount = report.WarningCount,
                                            sensors = report.SensorCount,
                                            actuators = report.ActuatorCount,
                                            errors,
                                            warnings,
                                            checksLiveHomeAssistant = false
                                        });
                                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                                }
                            } catch (Exception ex) {
                                context.Response.StatusCode = 500;
                                context.Response.ContentType = "application/json";
                                var errJson = System.Text.Json.JsonSerializer.Serialize(new {
                                    mode = "offline",
                                    error = ex.Message,
                                    checksLiveHomeAssistant = false
                                });
                                var errBytes = System.Text.Encoding.UTF8.GetBytes(errJson);
                                context.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/mapek/tick" && context.Request.HttpMethod == "POST") {
                            // MAPE-K tick endpoint. Dry-run is the default. Real Home Assistant
                            // execution is fail-closed and requires MAPEK_ALLOW_EXECUTION, an
                            // explicit dryRun=false request, TOKEN_HA, entity/service allowlists,
                            // and safety policy approval.
                            try {
                                string? body = null;
                                if (context.Request.HasEntityBody) {
                                    using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                    body = await reader.ReadToEndAsync();
                                }

                                // Goal repository from DI: json (default,
                                // hot-reloaded) or sqlite, selected by GOAL_REPOSITORY_PROVIDER.
                                var goalRepo = host.Services.GetRequiredService<SmartNode.Services.Goals.IGoalRepository>();

                                var simulator = host.Services.GetRequiredService<SmartNode.Services.Simulation.IFutureSimulator>();
                                var monitor = host.Services.GetRequiredService<SmartNode.Mapek.Monitoring.IMapekMonitorService>();
                                var analyzer = host.Services.GetRequiredService<SmartNode.Mapek.Analysis.IMapekAnalyzerService>();
                                var decisionLog = host.Services.GetRequiredService<SmartNode.Services.Decisions.IDecisionLog>();

                                // Step 1.6 — build the fail-closed execution gate from the environment.
                                // Real Home Assistant actuation requires MAPEK_ALLOW_EXECUTION + dryRun=false
                                // + TOKEN_HA + the action's entity/service on the operator allowlists.
                                // PR4 — merge non-secret persisted settings with the env-based
                                // safety controls. Fail-closed: invalid persisted values are ignored
                                // (warned) and can never open execution; persisted limits can only
                                // tighten. TOKEN_HA stays env-only (secret; never persisted).
                                var haToken = Environment.GetEnvironmentVariable("TOKEN_HA") ?? string.Empty;
                                var settingsStore = host.Services.GetRequiredService<SmartNode.Services.Settings.ISettingsStore>();
                                var persistedSettings = settingsStore.GetAll()
                                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
                                var resolvedSafety = SmartNode.Mapek.Execution.SafetySettingsResolver.Resolve(
                                    Environment.GetEnvironmentVariable, persistedSettings);
                                foreach (var settingWarning in resolvedSafety.Warnings)
                                    logger.LogWarning("Safety settings: {warning}", settingWarning);
                                var execSettings = resolvedSafety.Execution;
                                var executor = new SmartNode.Mapek.Execution.HttpHaActionExecutor(
                                    GetHaUrl(), haToken,
                                    host.Services.GetService<ILogger<SmartNode.Mapek.Execution.HttpHaActionExecutor>>());

                                // P7-A — safety primitives (kill switch / cooldown / rate limit) now
                                // come from the same resolver, so a persisted kill switch or a tighter
                                // persisted limit applies alongside the environment variables.
                                var safetyOptions = resolvedSafety.Safety;

                                // P7-B — shared execution-history singleton backing cooldown/rate-limit.
                                var execHistory = host.Services.GetRequiredService<SmartNode.Services.Execution.IExecutionHistory>();

                                // P1 — shared safety-event audit log singleton.
                                var safetyEventLog = host.Services.GetRequiredService<SmartNode.Services.Safety.ISafetyEventLog>();

                                var responsePayload = await SmartNode.Mapek.MapekTickEndpoint.BuildDryRunResponseAsync(
                                    body, monitor, simulator, goalRepo, analyzer, decisionLog, execSettings, executor,
                                    safety: safetyOptions, executionHistory: execHistory, safetyEventLog: safetyEventLog);

                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(responsePayload,
                                    new System.Text.Json.JsonSerializerOptions {
                                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                                    });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = 200;
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/mapek/tick failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/product/status" && context.Request.HttpMethod == "GET") {
                            // P7 — stable product status surface for dashboard/setup UI.
                            // Secrets-free: reports provider names, redacted paths, and HA
                            // configured booleans, never raw URLs or TOKEN_HA.
                            try {
                                var settingsStore = host.Services.GetRequiredService<SmartNode.Services.Settings.ISettingsStore>();
                                var persisted = settingsStore.GetAll()
                                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
                                var resolved = SmartNode.Mapek.Execution.SafetySettingsResolver.Resolve(
                                    Environment.GetEnvironmentVariable, persisted);
                                var result = SmartNode.Services.Product.ProductStatusEndpoint.Get(
                                    GetMode(),
                                    IsTruthy(Environment.GetEnvironmentVariable("MAPEK_AUTONOMOUS")),
                                    HaConn,
                                    goalRepoOptions,
                                    decisionLogOptions,
                                    settingsStoreOptions,
                                    execHistoryOptions,
                                    safetyEventOptions,
                                    resolved,
                                    Assembly.GetExecutingAssembly());
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions {
                                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                                    });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/product/status failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/product/status");
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/decisions" && context.Request.HttpMethod == "GET") {
                            // P7 — product-facing decision-log alias with a bounded limit
                            // parameter. The historical /api/mapek/decisions route remains
                            // unchanged below for backwards compatibility.
                            try {
                                var decisionLog = host.Services.GetRequiredService<SmartNode.Services.Decisions.IDecisionLog>();
                                var result = SmartNode.Services.Product.ProductDecisionsEndpoint.Get(
                                    decisionLog,
                                    context.Request.QueryString["limit"]);
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions {
                                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                                    });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/decisions failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/decisions");
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/mapek/decisions" && context.Request.HttpMethod == "GET") {
                            // Read-only view of the most recent MAPE-K tick decisions from the
                            // configured decision-log provider — newest first.
                            try {
                                var decisionLog = host.Services.GetRequiredService<SmartNode.Services.Decisions.IDecisionLog>();
                                var recent = decisionLog.GetRecent();
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(
                                    new { count = recent.Count, decisions = recent },
                                    new System.Text.Json.JsonSerializerOptions {
                                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                                    });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = 200;
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/mapek/decisions failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/safety" && context.Request.HttpMethod == "GET") {
                            // P2 — read-only safety posture + recent audit trail. Secrets-free:
                            // it exposes booleans, integer limits, and allowlist counts (not the
                            // token or the allowlisted names), plus the persisted safety-event log.
                            try {
                                var settingsStore = host.Services.GetRequiredService<SmartNode.Services.Settings.ISettingsStore>();
                                var persisted = settingsStore.GetAll()
                                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
                                var resolved = SmartNode.Mapek.Execution.SafetySettingsResolver.Resolve(
                                    Environment.GetEnvironmentVariable, persisted);
                                var eventLog = host.Services.GetRequiredService<SmartNode.Services.Safety.ISafetyEventLog>();
                                var result = SmartNode.Services.Safety.SafetyEndpoint.Get(
                                    resolved.Safety, resolved.Execution, eventLog, safetyEventOptions.Provider);
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions {
                                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                                    });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/safety failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/settings"
                                || context.Request.Url!.AbsolutePath.StartsWith("/api/settings/")) {
                            // Non-secret product settings CRUD. Values are persisted by the
                            // configured settings store, but they do not change execution gates or
                            // store HA credentials.
                            try {
                                var settingsStore = host.Services.GetRequiredService<SmartNode.Services.Settings.ISettingsStore>();
                                var path = context.Request.Url!.AbsolutePath;
                                var method = context.Request.HttpMethod;
                                SmartNode.Services.Settings.SettingsEndpointResult result;
                                if (path == "/api/settings" && method == "GET") {
                                    result = SmartNode.Services.Settings.SettingsEndpoint.List(settingsStore);
                                } else if (path == "/api/settings" && method == "PUT") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = SmartNode.Services.Settings.SettingsEndpoint.Update(settingsStore, body);
                                } else if (path.StartsWith("/api/settings/") && method == "GET") {
                                    var key = Uri.UnescapeDataString(path.Substring("/api/settings/".Length));
                                    result = SmartNode.Services.Settings.SettingsEndpoint.Get(settingsStore, key);
                                } else if (path.StartsWith("/api/settings/") && method == "PUT") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    var key = Uri.UnescapeDataString(path.Substring("/api/settings/".Length));
                                    result = SmartNode.Services.Settings.SettingsEndpoint.Set(settingsStore, key, body);
                                } else if (path.StartsWith("/api/settings/") && method == "DELETE") {
                                    var key = Uri.UnescapeDataString(path.Substring("/api/settings/".Length));
                                    result = SmartNode.Services.Settings.SettingsEndpoint.Delete(settingsStore, key);
                                } else {
                                    result = new SmartNode.Services.Settings.SettingsEndpointResult(405, new { error = "Method not allowed on this settings resource." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/settings failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/goals"
                                || context.Request.Url!.AbsolutePath.StartsWith("/api/goals/")) {
                            // Goal editor CRUD. GET works for any provider;
                            // POST/DELETE require a mutable provider (sqlite) — otherwise 409.
                            try {
                                var goalRepo = host.Services.GetRequiredService<SmartNode.Services.Goals.IGoalRepository>();
                                var path = context.Request.Url!.AbsolutePath;
                                var method = context.Request.HttpMethod;
                                SmartNode.Services.Goals.GoalEditorResult result;
                                if (path == "/api/goals" && method == "GET") {
                                    result = SmartNode.Services.Goals.GoalEditorEndpoint.List(goalRepo);
                                } else if (path == "/api/goals" && method == "POST") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = SmartNode.Services.Goals.GoalEditorEndpoint.Create(goalRepo, body);
                                } else if (path.StartsWith("/api/goals/") && method == "GET") {
                                    var id = Uri.UnescapeDataString(path.Substring("/api/goals/".Length));
                                    result = SmartNode.Services.Goals.GoalEditorEndpoint.GetById(goalRepo, id);
                                } else if (path.StartsWith("/api/goals/") && method == "PUT") {
                                    var id = Uri.UnescapeDataString(path.Substring("/api/goals/".Length));
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = SmartNode.Services.Goals.GoalEditorEndpoint.Replace(goalRepo, id, body);
                                } else if (path.StartsWith("/api/goals/") && method == "DELETE") {
                                    var id = Uri.UnescapeDataString(path.Substring("/api/goals/".Length));
                                    result = SmartNode.Services.Goals.GoalEditorEndpoint.Delete(goalRepo, id);
                                } else if (path.StartsWith("/api/goals/") && method == "PATCH") {
                                    // Enable/disable toggle: body {"enabled": true|false}.
                                    var id = Uri.UnescapeDataString(path.Substring("/api/goals/".Length));
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = SmartNode.Services.Goals.GoalEditorEndpoint.SetEnabled(goalRepo, id, body);
                                } else {
                                    result = new SmartNode.Services.Goals.GoalEditorResult(405, new { error = "Method not allowed on this goals resource." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/goals failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/bindings/validate") {
                            // P4 — validate an EDITED binding config (request body) offline,
                            // before it is adopted. POST only; runs the same static checks as
                            // the file validator and never reads/writes files or calls HA.
                            try {
                                SmartNode.Validation.BindingsValidationEndpointResult result;
                                if (context.Request.HttpMethod == "POST") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = SmartNode.Validation.BindingsConfigValidationEndpoint.Validate(body);
                                } else {
                                    result = new SmartNode.Validation.BindingsValidationEndpointResult(405, new { error = "Method not allowed. Use POST /api/ha/bindings/validate." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var validateBytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(validateBytes, 0, validateBytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/ha/bindings/validate failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var validateBytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/ha/bindings/validate");
                                context.Response.OutputStream.Write(validateBytes, 0, validateBytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/bindings/adopt") {
                            // PR A2 — DRY-RUN-ONLY bindings adoption. POST only. Proves the adopt
                            // contract (validation, stale-write hash guard, HTTP statuses, dry-run
                            // response) with ZERO side effect: no disk write, no backup, no revision
                            // store, no HA_BINDINGS_FILE change, no Home Assistant call. dryRun:false
                            // is refused with 501 in this slice.
                            try {
                                SmartNode.Services.Bindings.BindingsAdoptionEndpointResult result;
                                if (context.Request.HttpMethod == "POST") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    // Read the active runtime bindings file (same resolution as Factory),
                                    // empty string when none exists. Read-only — never written here.
                                    string currentConfigRaw = "";
                                    try {
                                        var adoptBindingsPath = Environment.GetEnvironmentVariable("HA_BINDINGS_FILE");
                                        if (string.IsNullOrWhiteSpace(adoptBindingsPath))
                                            adoptBindingsPath = SmartNode.HaBindings.HaBindingsLoader.ResolveDefaultShowcasePath();
                                        if (!string.IsNullOrWhiteSpace(adoptBindingsPath) && System.IO.File.Exists(adoptBindingsPath))
                                            currentConfigRaw = await System.IO.File.ReadAllTextAsync(adoptBindingsPath);
                                    } catch { /* treat as no current config */ }
                                    result = SmartNode.Services.Bindings.BindingsAdoptionEndpoint.Adopt(body, currentConfigRaw);
                                } else {
                                    result = new SmartNode.Services.Bindings.BindingsAdoptionEndpointResult(405, new { error = "Method not allowed. Use POST /api/ha/bindings/adopt." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var adoptBytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(adoptBytes, 0, adoptBytes.Length);
                            } catch (Exception ex) {
                                logger.LogWarning("/api/ha/bindings/adopt failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var adoptBytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/ha/bindings/adopt");
                                context.Response.OutputStream.Write(adoptBytes, 0, adoptBytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/discovery/draft/export") {
                            // P4-D runtime config preview. POST only; converts the submitted
                            // P4-C draft offline, validates in memory, and never reads/writes HA
                            // bindings files or calls Home Assistant.
                            try {
                                SmartNode.Services.HomeAssistant.HaBindingConfigExportResult result;
                                if (context.Request.HttpMethod == "POST") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = SmartNode.Services.HomeAssistant.HaBindingConfigExporter.ExportFromBody(body);
                                } else {
                                    result = new SmartNode.Services.HomeAssistant.HaBindingConfigExportResult(405, new { error = "Method not allowed." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var exportBytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(exportBytes, 0, exportBytes.Length);
                            } catch (Exception ex) {
                                // Defensive: never include the token in an error body or log.
                                logger.LogWarning("/api/ha/discovery/draft/export failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var exportBytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/ha/discovery/draft/export");
                                context.Response.OutputStream.Write(exportBytes, 0, exportBytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/discovery/draft") {
                            // P4-C setup discovery selection. POST only; returns a review-only
                            // binding draft and never calls HA services or persists anything.
                            try {
                                SmartNode.Services.HomeAssistant.HaBindingDraftResult result;
                                if (context.Request.HttpMethod == "POST") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = await SmartNode.Services.HomeAssistant.HaBindingDraftBuilder.BuildAsync(
                                        body,
                                        HaConn,
                                        new SmartNode.Services.HomeAssistant.HttpHaCatalogReader());
                                } else {
                                    result = new SmartNode.Services.HomeAssistant.HaBindingDraftResult(405, new { error = "Method not allowed." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var draftBytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(draftBytes, 0, draftBytes.Length);
                            } catch (Exception ex) {
                                // Defensive: never include the token in an error body or log.
                                logger.LogWarning("/api/ha/discovery/draft failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var draftBytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/ha/discovery/draft");
                                context.Response.OutputStream.Write(draftBytes, 0, draftBytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/discovery") {
                            // P4-B setup discovery. GET only. The tested endpoint owns all
                            // grouping/capability/warning logic; Program.cs stays a thin adapter.
                            try {
                                SmartNode.Services.HomeAssistant.HaDiscoveryResult result;
                                if (context.Request.HttpMethod == "GET") {
                                    result = await SmartNode.Services.HomeAssistant.HaDiscoveryEndpoint.BuildAsync(
                                        HaConn,
                                        new SmartNode.Services.HomeAssistant.HttpHaCatalogReader());
                                } else {
                                    result = new SmartNode.Services.HomeAssistant.HaDiscoveryResult(405, new { error = "Method not allowed." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var discBytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(discBytes, 0, discBytes.Length);
                            } catch (Exception ex) {
                                // Defensive: never include the token in an error body or log.
                                logger.LogWarning("/api/ha/discovery failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var discBytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/ha/discovery");
                                context.Response.OutputStream.Write(discBytes, 0, discBytes.Length);
                            }
                        } else if (context.Request.Url!.AbsolutePath == "/api/ha/connection") {
                            // P4-A setup wizard. GET = status (never the token). POST = test+store.
                            try {
                                var method = context.Request.HttpMethod;
                                SmartNode.Services.HomeAssistant.HaConnectionResult result;
                                if (method == "GET") {
                                    result = SmartNode.Services.HomeAssistant.HaConnectionEndpoint.Status(HaConn);
                                } else if (method == "POST") {
                                    string? body = null;
                                    if (context.Request.HasEntityBody) {
                                        using var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                                        body = await reader.ReadToEndAsync();
                                    }
                                    result = await SmartNode.Services.HomeAssistant.HaConnectionEndpoint.TestAndStoreAsync(
                                        body, new SmartNode.Services.HomeAssistant.HttpHaProbe(), HaConn);
                                } else {
                                    result = new SmartNode.Services.HomeAssistant.HaConnectionResult(405, new { error = "Method not allowed." });
                                }
                                var jsonOut = System.Text.Json.JsonSerializer.Serialize(result.Payload,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                                var connBytes = System.Text.Encoding.UTF8.GetBytes(jsonOut);
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = result.StatusCode;
                                context.Response.OutputStream.Write(connBytes, 0, connBytes.Length);
                            } catch (Exception ex) {
                                // Defensive: never include the token in an error body or log.
                                logger.LogWarning("/api/ha/connection failed: {msg}", ex.Message);
                                context.Response.StatusCode = 500;
                                var connBytes = System.Text.Encoding.UTF8.GetBytes("Internal error handling /api/ha/connection");
                                context.Response.OutputStream.Write(connBytes, 0, connBytes.Length);
                            }
                        }
                        context.Response.Close();
                        } catch (Exception handlerEx) {
                            logger.LogError(handlerEx, "Request handler crashed");
                            try {
                                context.Response.StatusCode = 500;
                                context.Response.Close();
                            } catch { /* response may already be closed */ }
                        }
                        });
                    }
                } catch (Exception ex) {
                    logger.LogError(ex, "Failed to start internal API");
                }
            });

            // Step 1.8 — optional autonomous MAPE-K tick (DRY-RUN ONLY). Disabled by
            // default; enabled with MAPEK_AUTONOMOUS=true. Runs on its own task so it
            // never blocks the HTTP API or the MAPE-K loop below, and stops cleanly on
            // Ctrl+C / process exit. Execution is hard-disabled in this loop — it only
            // observes, analyzes, scores, plans, and writes the decision log.
            var autoOptions = SmartNode.Mapek.Autonomous.AutonomousTickOptions.Parse(Environment.GetEnvironmentVariable);
            using var autoCts = new CancellationTokenSource();
            if (autoOptions.Enabled)
            {
                Console.CancelKeyPress += (_, _) => { try { autoCts.Cancel(); } catch { /* already disposed */ } };
                AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { autoCts.Cancel(); } catch { /* already disposed */ } };

                var autoMonitor = host.Services.GetRequiredService<SmartNode.Mapek.Monitoring.IMapekMonitorService>();
                var autoSimulator = host.Services.GetRequiredService<SmartNode.Services.Simulation.IFutureSimulator>();
                var autoAnalyzer = host.Services.GetRequiredService<SmartNode.Mapek.Analysis.IMapekAnalyzerService>();
                var autoDecisionLog = host.Services.GetRequiredService<SmartNode.Services.Decisions.IDecisionLog>();

                // Goals from the same DI provider as the manual /api/mapek/tick endpoint.
                var autoGoalRepo = host.Services.GetRequiredService<SmartNode.Services.Goals.IGoalRepository>();

                var autoTick = SmartNode.Mapek.Autonomous.AutonomousTick.CreateDryRunTick(
                    autoMonitor, autoSimulator, autoGoalRepo, autoAnalyzer, autoDecisionLog);
                var autoRunner = new SmartNode.Mapek.Autonomous.AutonomousTickRunner(
                    autoTick, autoOptions.Interval,
                    host.Services.GetService<ILogger<SmartNode.Mapek.Autonomous.AutonomousTickRunner>>());

                logger.LogInformation(
                    "MAPE-K autonomous mode ENABLED (dry-run only, interval {sec}s).", autoOptions.Interval.TotalSeconds);

                // PR4 — the mapek.autonomous_execution_enabled setting (env mirror
                // MAPEK_AUTONOMOUS_EXECUTION) is recognised and resolved fail-closed, but the
                // autonomous loop stays dry-run in this build: real autonomous actuation is a
                // separate, later step. Warn so an operator who flips it is not misled.
                var autoSettingsStore = host.Services.GetRequiredService<SmartNode.Services.Settings.ISettingsStore>();
                var autoPersisted = autoSettingsStore.GetAll()
                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
                var autoResolved = SmartNode.Mapek.Execution.SafetySettingsResolver.Resolve(
                    Environment.GetEnvironmentVariable, autoPersisted);
                if (autoResolved.AutonomousExecutionEnabled)
                {
                    logger.LogWarning(
                        "mapek.autonomous_execution_enabled is set, but autonomous real execution is not enabled in this build; the autonomous loop remains dry-run.");
                }

                _ = Task.Run(() => autoRunner.RunAsync(autoCts.Token));
            }

            // Mode dispatch:
            //   "full"         → start the MAPE-K loop normally (showcase demo).
            //   "chatbox-only" → skip MAPE-K so the chatbox can run against an arbitrary HA
            //                    without the showcase entity bindings in Factory.cs being invoked.
            var mode = GetMode();
            var rawMode = Environment.GetEnvironmentVariable("SMARTNODE_MODE");
            if (!string.IsNullOrWhiteSpace(rawMode) && !string.Equals(rawMode.Trim(), mode, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Unknown SMARTNODE_MODE='{raw}', defaulting to 'full'. Valid values: full, chatbox-only.", rawMode);
            }
            logger.LogInformation("Starting SmartNode in '{mode}' mode (HA_URL={ha}).", mode, GetHaUrl());

            if (mode == "chatbox-only")
            {
                logger.LogInformation("Chatbox-only mode: MAPE-K loop skipped (set SMARTNODE_MODE=full to enable). HTTP API + chatbox remain active.");
            }
            else
            {
                // MAPE-K needs live HA sensors; if HA is down we log and keep the HTTP API alive
                // so the chatbox remains usable for non-MAPE-K work.
                try
                {
                    var mapekManager = host.Services.GetRequiredService<IMapekManager>();
                    await mapekManager.StartLoop();
                    logger.LogInformation("MAPE-K ended normally.");
                }
                catch (Exception exception)
                {
                    logger.LogCritical(exception, "MAPE-K loop failed — HTTP API continues. Fix HA then restart SmartNode to recover MAPE-K, or run with SMARTNODE_MODE=chatbox-only to skip MAPE-K entirely.");
                }
            }

            // Keep the process alive so the background HTTP listener (chatbox API) stays up.
            await Task.Delay(Timeout.Infinite);
            return 0;
        }
    }
}

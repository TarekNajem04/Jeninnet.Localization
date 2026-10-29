//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests.Performance;

/// <summary>
/// Non-gating evidence-based performance measurements for the localization runtime. These tests
/// record reproducible throughput and allocation data for representative lookup paths and never
/// assert timing thresholds; they are filtered separately via TestCategory=Performance and do not
/// run in parallel with the contract suite.
/// </summary>
[TestClass]
[TestCategory("Performance")]
[DoNotParallelize]
public class PerformanceEvidenceTests {
    private const int TIME_BLOCKS = 7;
    private const int INIT_BLOCKS = 5;
    private const int OPS_MEMORY_EXACT = 50_000;
    private const int OPS_MEMORY_MISS = 20_000;
    private const int OPS_STEADY_FILE = 20_000;
    private const int OPS_RES_X = 10_000;
    private const int OPS_CREATE = 1_000;
    private const int OPS_BRIDGE_LOOKUP = 20_000;
    private const int OPS_SQLITE = 100;

    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that environment prints reproducible snapshot.
    /// </summary>
    [TestMethod]
    public void Environment_PrintsReproducibleSnapshot() {
        Emit("ENV runtime=" + RuntimeInformation.FrameworkDescription);
        Emit("ENV os=" + RuntimeInformation.OSDescription);
        Emit("ENV processArch=" + RuntimeInformation.ProcessArchitecture + " osArch=" + RuntimeInformation.OSArchitecture);
        Emit("ENV processors=" + Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture));
        Emit("ENV machine=" + Environment.MachineName);
        Emit("ENV pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        Emit("ENV dotnetVersion=" + Environment.Version);
        Emit("ENV serverGC=" + System.Runtime.GCSettings.IsServerGC.ToString());
        Emit("ENV currentCulture=" + CultureInfo.CurrentCulture.Name + " currentUICulture=" + CultureInfo.CurrentUICulture.Name);
        Emit("ENV stopwatchHighRes=" + Stopwatch.IsHighResolution.ToString() + " frequencyHz=" + Stopwatch.Frequency.ToString(CultureInfo.InvariantCulture));

        Assert.IsFalse(string.IsNullOrEmpty(RuntimeInformation.FrameworkDescription), "the environment snapshot must record the runtime");
        Assert.IsTrue(Stopwatch.IsHighResolution, "the harness requires a high-resolution monotonic timer");
    }

    /// <summary>
    /// Verifies that memory providers and core lookup repeated steady state.
    /// </summary>
    [TestMethod]
    public async Task Memory_ProvidersAndCoreLookup_RepeatedSteadyStateAsync() {
        const int keyCount = 1000;
        var messages = BuildMessages(keyCount);
        var memoryProvider = new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = messages });

        var services = new ServiceCollection();
        services.AddMemoryLocalization(messages);
        services.AddLocalization(static options => options.DefaultCulture = "en");
        await using var serviceProvider = services.BuildServiceProvider();
        var localizer = serviceProvider.GetRequiredService<IMessageLocalizer>();

        var presentKey = KeyFor(keyCount / 2);
        const string missingKey = "perf.key.missing";
        var culture = new CultureInfo("en");
        var requestCulture = new CultureInfo("fr-FR");
        var expectedTemplate = TemplateFor(keyCount / 2);

        var directValue = await memoryProvider.GetMessageTemplateAsync(presentKey, culture, CancellationToken.None);
        var localizerValue = await localizer.LocalizeAsync(presentKey, _noParameters, culture, CancellationToken.None);
        var missingValue = await localizer.LocalizeAsync(missingKey, _noParameters, culture, CancellationToken.None);
        var fallbackValue = await localizer.LocalizeAsync(presentKey, _noParameters, requestCulture, CancellationToken.None);
        Assert.AreEqual(expectedTemplate, directValue, "the provider-direct measurement must exercise a present key");
        Assert.AreEqual(expectedTemplate, localizerValue, "the core localizer measurement must exercise a present key");
        Assert.IsNull(missingValue, "the missing-key measurement must exercise a genuine miss");
        Assert.AreEqual(expectedTemplate, fallbackValue, "the fallback measurement must resolve through the default culture");

        var providerDirect = await MeasureAsync(
            TIME_BLOCKS,
            OPS_MEMORY_EXACT,
            () => DiscardAsync(memoryProvider.GetMessageTemplateAsync(presentKey, culture, CancellationToken.None)));
        EmitResult("Memory provider | direct lookup | exact present", keyCount, OPS_MEMORY_EXACT, TIME_BLOCKS, providerDirect);
        AssertMeasurementValid(providerDirect);

        var coreExact = await MeasureAsync(
            TIME_BLOCKS,
            OPS_MEMORY_EXACT,
            () => DiscardAsync(localizer.LocalizeAsync(presentKey, _noParameters, culture, CancellationToken.None)));
        EmitResult("Core IMessageLocalizer | exact present", keyCount, OPS_MEMORY_EXACT, TIME_BLOCKS, coreExact);

        var coreMissing = await MeasureAsync(
            TIME_BLOCKS,
            OPS_MEMORY_MISS,
            () => DiscardAsync(localizer.LocalizeAsync(missingKey, _noParameters, culture, CancellationToken.None)));
        EmitResult("Core IMessageLocalizer | missing key", keyCount, OPS_MEMORY_MISS, TIME_BLOCKS, coreMissing);

        var coreFallback = await MeasureAsync(
            TIME_BLOCKS,
            OPS_MEMORY_MISS,
            () => DiscardAsync(localizer.LocalizeAsync(presentKey, _noParameters, requestCulture, CancellationToken.None)));
        EmitResult("Core IMessageLocalizer | culture fallback fr-FR -> en", keyCount, OPS_MEMORY_MISS, TIME_BLOCKS, coreFallback);
    }

    /// <summary>
    /// Verifies that file providers first lookup initialization.
    /// </summary>
    [TestMethod]
    public async Task FileProviders_FirstLookup_InitializationAsync() {
        var sizeValues = new int[3] { 10, 1000, 10000 };
        var scratchRoot = Path.Combine(Path.GetTempPath(), "jeninnet-perf-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]);
        Directory.CreateDirectory(scratchRoot);

        try {
            foreach(var size in sizeValues) {
                var sizeLabel = size.ToString(CultureInfo.InvariantCulture);
                var items = BuildMessages(size);
                var firstKey = KeyFor(0);
                var culture = new CultureInfo("en");

                var jsonPath = Path.Combine(scratchRoot, "messages-" + sizeLabel + ".json");
                await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(items), TestContext?.CancellationToken ?? CancellationToken.None);
                var jsonProbe = await new JsonMessageProvider(jsonPath).GetMessageTemplateAsync(firstKey, culture, CancellationToken.None);
                Assert.AreEqual(TemplateFor(0), jsonProbe, "the JSON initialization measurement must resolve seeded data");
                var jsonInit = await MeasureAsync(
                    INIT_BLOCKS,
                    1,
                    () => {
                        var provider = new JsonMessageProvider(jsonPath);
                        return DiscardAsync(provider.GetMessageTemplateAsync(firstKey, culture, CancellationToken.None));
                    },
                    warmupOverride: 1);
                EmitResult("JSON provider | first lookup (parse + load) | exact present", size, 1, INIT_BLOCKS, jsonInit);

                var xmlPath = Path.Combine(scratchRoot, "messages-" + sizeLabel + ".xml");
                WriteXmlFixture(xmlPath, items);
                var xmlProbe = await new XmlMessageProvider(xmlPath).GetMessageTemplateAsync(firstKey, culture, CancellationToken.None);
                Assert.AreEqual(TemplateFor(0), xmlProbe, "the XML initialization measurement must resolve seeded data");
                var xmlInit = await MeasureAsync(
                    INIT_BLOCKS,
                    1,
                    () => {
                        var provider = new XmlMessageProvider(xmlPath);
                        return DiscardAsync(provider.GetMessageTemplateAsync(firstKey, culture, CancellationToken.None));
                    },
                    warmupOverride: 1);
                EmitResult("XML provider | first lookup (parse + load) | exact present", size, 1, INIT_BLOCKS, xmlInit);
            }
        }
        finally {
            Directory.Delete(scratchRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies that file providers repeated lookup steady state.
    /// </summary>
    [TestMethod]
    public async Task FileProviders_RepeatedLookup_SteadyStateAsync() {
        var sizeValues = new int[2] { 1000, 10000 };
        var scratchRoot = Path.Combine(Path.GetTempPath(), "jeninnet-perf-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]);
        Directory.CreateDirectory(scratchRoot);

        try {
            foreach(var size in sizeValues) {
                var sizeLabel = size.ToString(CultureInfo.InvariantCulture);
                var items = BuildMessages(size);
                var jsonPath = Path.Combine(scratchRoot, "messages-" + sizeLabel + ".json");
                var xmlPath = Path.Combine(scratchRoot, "messages-" + sizeLabel + ".xml");
                await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(items), TestContext?.CancellationToken ?? CancellationToken.None);
                WriteXmlFixture(xmlPath, items);

                var jsonProvider = new JsonMessageProvider(jsonPath);
                var xmlProvider = new XmlMessageProvider(xmlPath);
                var key = KeyFor(size / 2);
                var culture = new CultureInfo("en");

                var jsonValue = await jsonProvider.GetMessageTemplateAsync(key, culture, CancellationToken.None);
                var xmlValue = await xmlProvider.GetMessageTemplateAsync(key, culture, CancellationToken.None);
                Assert.AreEqual(TemplateFor(size / 2), jsonValue, "the JSON steady-state measurement must resolve seeded data");
                Assert.AreEqual(TemplateFor(size / 2), xmlValue, "the XML steady-state measurement must resolve seeded data");

                var jsonSteady = await MeasureAsync(
                    TIME_BLOCKS,
                    OPS_STEADY_FILE,
                    () => DiscardAsync(jsonProvider.GetMessageTemplateAsync(key, culture, CancellationToken.None)));
                EmitResult("JSON provider | repeated lookup | exact present | steady state", size, OPS_STEADY_FILE, TIME_BLOCKS, jsonSteady);
                AssertMeasurementValid(jsonSteady);

                var xmlSteady = await MeasureAsync(
                    TIME_BLOCKS,
                    OPS_STEADY_FILE,
                    () => DiscardAsync(xmlProvider.GetMessageTemplateAsync(key, culture, CancellationToken.None)));
                EmitResult("XML provider | repeated lookup | exact present | steady state", size, OPS_STEADY_FILE, TIME_BLOCKS, xmlSteady);
            }
        }
        finally {
            Directory.Delete(scratchRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies that res XProvider exact culture repeated lookup.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_ExactCulture_RepeatedLookupAsync() {
        var options = new ResXLocalizationOptions {
            BaseName = "Jeninnet.Localization.Tests.TestResources",
            ResourceAssembly = typeof(PerformanceEvidenceTests).Assembly
        };
        var provider = new ResXMessageProvider(options);
        const string key = "Greeting";
        var culture = new CultureInfo("en");

        var greetingValue = await provider.GetMessageTemplateAsync(key, culture, CancellationToken.None);
        Assert.IsNotNull(greetingValue, "the RESX measurement must resolve the embedded Greeting value for the exact culture");

        var keyCount = 0;
        var manager = new ResourceManager(options.BaseName, options.ResourceAssembly);
        var exactSet = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        if(exactSet is not null) {
            var enumerator = exactSet.GetEnumerator();
            while(enumerator.MoveNext()) {
                keyCount++;
            }
        }

        var result = await MeasureAsync(
            TIME_BLOCKS,
            OPS_RES_X,
            () => DiscardAsync(provider.GetMessageTemplateAsync(key, culture, CancellationToken.None)));
        EmitResult("RESX provider | repeated lookup | exact present | embedded set", keyCount, OPS_RES_X, TIME_BLOCKS, result);
        AssertMeasurementValid(result);
    }

    /// <summary>
    /// Verifies that store bridge string localizer create and lookup.
    /// </summary>
    [TestMethod]
    public void StoreBridge_StringLocalizer_CreateAndLookup() {
        const string resourceSet = "perfset";
        const int keyCount = 1000;
        var store = new PerfStore();
        for(var i = 0; i < keyCount; i++) {
            store.SetEntry(resourceSet, "en", KeyFor(i), TemplateFor(i));
        }

        var factory = new CustomStringLocalizerFactory(store);

        RunWithCurrentUiCulture("en", () => {
            var create = Measure(
                TIME_BLOCKS,
                OPS_CREATE,
                () => _ = factory.Create(resourceSet, "performance")
            );
            EmitResult("Store bridge | IStringLocalizerFactory.Create(string,string)", keyCount, OPS_CREATE, TIME_BLOCKS, create);
            AssertMeasurementValid(create);

            var localizer = factory.Create(resourceSet, "performance");
            var key = KeyFor(keyCount / 2);
            Assert.AreEqual(TemplateFor(keyCount / 2), localizer[key].Value, "the store bridge lookup must resolve seeded data");
            var lookup = Measure(
                TIME_BLOCKS,
                OPS_BRIDGE_LOOKUP,
                () => _ = localizer[key].Value
            );
            EmitResult("Store bridge | IStringLocalizer indexer lookup | exact present", keyCount, OPS_BRIDGE_LOOKUP, TIME_BLOCKS, lookup);
            AssertMeasurementValid(lookup);
        });
    }

    /// <summary>
    /// Verifies that sqlite message provider in memory database repeated lookup.
    /// </summary>
    [TestMethod]
    public async Task SqliteMessageProvider_InMemoryDatabase_RepeatedLookupAsync() {
        const int keyCount = 1000;
        var dataSource = "jeninnet-perf-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var connectionString = "Data Source=" + dataSource + ";Mode=Memory;Cache=Shared";

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext?.CancellationToken ?? CancellationToken.None);
        await CreateAndSeedLocalizationMessagesAsync(connection, keyCount);

        var provider = new SqliteMessageProvider(new SqliteLocalizationOptions {
            ConnectionString = connectionString,
            TableName = "LocalizationMessages"
        });
        var key = KeyFor(keyCount / 2);
        var culture = new CultureInfo("en");

        var seededValue = await provider.GetMessageTemplateAsync(key, culture, CancellationToken.None);
        Assert.AreEqual(TemplateFor(keyCount / 2), seededValue, "the SQLite measurement must resolve seeded data through the real database path");

        var result = await MeasureAsync(
            INIT_BLOCKS,
            OPS_SQLITE,
            () => DiscardAsync(provider.GetMessageTemplateAsync(key, culture, CancellationToken.None)),
            warmupOverride: 20);
        EmitResult("SQLite provider (in-memory DB) | repeated lookup | exact present | per-call connection + query", keyCount, OPS_SQLITE, INIT_BLOCKS, result);
        AssertMeasurementValid(result);
    }

    /// <summary>
    /// Verifies that diagnostics absent versus observed lookup overhead evidence.
    /// </summary>
    [TestMethod]
    public async Task Diagnostics_AbsentVersusObserved_LookupOverheadEvidenceAsync() {
        const int opsPerBlock = 5_000;
        const int blocks = 5;
        var messages = BuildMessages(1000);
        var services = new ServiceCollection();
        services.AddMemoryLocalization(messages);
        services.AddLocalization(static options => options.DefaultCulture = "en");
        await using var serviceProvider = services.BuildServiceProvider();
        var localizer = serviceProvider.GetRequiredService<IMessageLocalizer>();

        var key = KeyFor(500);
        var culture = new CultureInfo("en");
        Emit("ENV diagnosticsMeterInstruments=" + Jeninnet.Localization.Abstractions.Diagnostics.LocalizationDiagnostics.Meter.Name);

        var absent = await MeasureAsync(
            blocks,
            opsPerBlock,
            () => DiscardAsync(localizer.LocalizeAsync(key, _noParameters, culture, CancellationToken.None)));
        EmitResult("Core IMessageLocalizer | diagnostics absent (no listeners)", 1000, opsPerBlock, blocks, absent);
        AssertMeasurementValid(absent);

        var activities = new List<Activity>();
        var measurements = new List<ObservabilityTestHarness.InstrumentMeasurement>();
        using var activityListener = ObservabilityTestHarness.CreateActivityListener(activities);
        using var meterListener = ObservabilityTestHarness.CreateMeterListener(measurements);

        var observed = await MeasureAsync(
            blocks,
            opsPerBlock,
            () => DiscardAsync(localizer.LocalizeAsync(key, _noParameters, culture, CancellationToken.None)));
        EmitResult("Core IMessageLocalizer | diagnostics observed (activity + meter listeners recording)", 1000, opsPerBlock, blocks, observed);
        AssertMeasurementValid(observed);

        Emit(string.Format(
            CultureInfo.InvariantCulture,
            "NOTE | observed/absent median ratio={0:F2} | activities={1} | measurements={2} | non-gating evidence: observers pay the recording cost, non-observers do not",
            observed.MedianNs / absent.MedianNs,
            activities.Count,
            measurements.Count));
    }

    /// <summary>
    /// Performs the discard operation.
    /// </summary>
    /// <param name="task">The task.</param>
    private static async ValueTask DiscardAsync(ValueTask<string?> task) => _ = await task.ConfigureAwait(false);

    /// <summary>
    /// Builds messages.
    /// </summary>
    /// <param name="keyCount">The key count.</param>
    /// <returns>The messages result.</returns>
    private static Dictionary<string, string> BuildMessages(int keyCount) {
        var messages = new Dictionary<string, string>(keyCount);
        for(var i = 0; i < keyCount; i++) {
            messages[KeyFor(i)] = TemplateFor(i);
        }

        return messages;
    }

    /// <summary>
    /// Performs the key for operation.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The key for text.</returns>
    private static string KeyFor(int index) => "perf.key." + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Performs the template for operation.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The template for text.</returns>
    private static string TemplateFor(int index) => "sample content for performance key " + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes xml fixture.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="items">The items.</param>
    private static void WriteXmlFixture(string path, IReadOnlyDictionary<string, string> items) =>
        new XDocument(new XElement("messages", items.Select(static pair => new XElement(pair.Key, pair.Value)))).Save(path);

    /// <summary>
    /// Creates and seed localization messages.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="keyCount">The key count.</param>
    private async Task CreateAndSeedLocalizationMessagesAsync(SqliteConnection connection, int keyCount) {
        await using(var create = connection.CreateCommand()) {
            create.CommandText = "CREATE TABLE IF NOT EXISTS \"LocalizationMessages\" (\"MessageKey\" TEXT NOT NULL, \"Culture\" TEXT NOT NULL, \"Template\" TEXT NOT NULL);";
            await create.ExecuteNonQueryAsync(TestContext?.CancellationToken ?? CancellationToken.None).ConfigureAwait(false);
        }

        await using var transaction = await connection.BeginTransactionAsync(TestContext?.CancellationToken ?? CancellationToken.None).ConfigureAwait(false);
        await using(var insert = connection.CreateCommand()) {
            insert.CommandText = "INSERT INTO \"LocalizationMessages\" (\"MessageKey\",\"Culture\",\"Template\") VALUES (@MessageKey,'en',@Template);";
            insert.Transaction = (SqliteTransaction)transaction;
            var keyParameter = insert.CreateParameter();
            keyParameter.ParameterName = "@MessageKey";
            insert.Parameters.Add(keyParameter);
            var templateParameter = insert.CreateParameter();
            templateParameter.ParameterName = "@Template";
            insert.Parameters.Add(templateParameter);

            for(var i = 0; i < keyCount; i++) {
                keyParameter.Value = KeyFor(i);
                templateParameter.Value = TemplateFor(i);
                await insert.ExecuteNonQueryAsync(TestContext?.CancellationToken ?? CancellationToken.None).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(TestContext?.CancellationToken ?? CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores perf.
    /// </summary>
    private sealed class PerfStore : ILocalizationStore {
        private readonly Dictionary<(string Set, string Culture), PerfResourceSet> _sets = [];

        /// <summary>
        /// Sets entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        public void SetEntry(string resourceSet, string culture, string key, string value) {
            if(!_sets.TryGetValue((resourceSet, culture), out var set)) {
                set = new PerfResourceSet(resourceSet, culture);
                _sets[(resourceSet, culture)] = set;
            }

            set.SetEntry(key, value);
        }

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            if(_sets.TryGetValue((resourceSet, culture), out var set)) {
                localizationResourceSet = set;
                return true;
            }

            localizationResourceSet = null;
            return false;
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            TryGetResourceSet(resourceSet, culture, out var set);
            return Task.FromResult(set);
        }

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
            _ = culture;
            _ = cancellationToken;
            return Task.FromResult(new List<ILocalizationResourceSet>());
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) {
            if(TryGetResourceSet(resourceSet, culture, out var set)) {
                return set.GetStringAsync(name, cancellationToken);
            }

            return Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name));
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            if(TryGetResourceSet(resourceSet, culture, out var set)) {
                return set.GetAllStringsAsync(cancellationToken);
            }

            return Task.FromResult(new Dictionary<string, LocalizedStringEntry>());
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            _ = culture;
            _ = cancellationToken;
            return Task.FromResult(new Dictionary<string, LocalizedStringEntry>());
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
            if(TryGetResourceSet(resourceSet, culture, out var set)) {
                return set.TryGetString(name, out entry);
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            return Task.FromResult(new List<string>());
        }
    }

    /// <summary>
    /// Represents a perf resource set.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    private sealed class PerfResourceSet(string resourceSet, string culture) : ILocalizationResourceSet {
        private readonly Dictionary<string, string> _entries = [];

        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => resourceSet;

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => culture;

        /// <summary>
        /// Sets entry.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        public void SetEntry(string key, string value) => _entries[key] = value;

        /// <summary>
        /// Gets culture info.
        /// </summary>
        /// <returns>The culture info result.</returns>
        public CultureInfo GetCultureInfo() => new(culture);

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            if(TryGetString(name, out var entry)) {
                return Task.FromResult(entry);
            }

            return Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name));
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            var result = new Dictionary<string, LocalizedStringEntry>(_entries.Count);
            foreach(var pair in _entries) {
                _ = TryGetString(pair.Key, out var entry);
                result[pair.Key] = entry!;
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
            if(_entries.TryGetValue(name, out var value)) {
                entry = new LocalizedStringEntry(Guid.Empty, resourceSet, culture, name, value);
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            _ = entry;
            _ = cancellationToken;
            throw new NotSupportedException("single add is not supported by performance resource sets");
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            _ = entries;
            _ = cancellationToken;
            throw new NotSupportedException("bulk add is not supported by performance resource sets");
        }

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            _ = entry;
            _ = cancellationToken;
            throw new NotSupportedException("single update is not supported by performance resource sets");
        }

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            _ = entries;
            _ = cancellationToken;
            throw new NotSupportedException("bulk update is not supported by performance resource sets");
        }

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            _ = entry;
            _ = cancellationToken;
            throw new NotSupportedException("single delete is not supported by performance resource sets");
        }

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            _ = entries;
            _ = cancellationToken;
            throw new NotSupportedException("bulk delete is not supported by performance resource sets");
        }
    }

    /// <summary>
    /// Performs the measure operation.
    /// </summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="opsPerBlock">The ops per block.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="warmupOverride">The warmup override.</param>
    /// <returns>The measure result.</returns>
    private static Measurement Measure(int blocks, int opsPerBlock, Action operation, int? warmupOverride = null) {
        var warmup = warmupOverride ?? Math.Max(1, opsPerBlock / 10);
        for(var i = 0; i < warmup; i++) {
            operation();
        }

        var samples = new double[blocks];
        var allocationSamples = new double[blocks];
        for(var block = 0; block < blocks; block++) {
            var startAllocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for(var i = 0; i < opsPerBlock; i++) {
                operation();
            }

            var end = Stopwatch.GetTimestamp();
            var endAllocated = GC.GetAllocatedBytesForCurrentThread();
            samples[block] = (end - start) / (double)Stopwatch.Frequency * 1_000_000_000.0 / opsPerBlock;
            allocationSamples[block] = (endAllocated - startAllocated) / (double)opsPerBlock;
        }

        return new Measurement(samples, allocationSamples);
    }

    /// <summary>
    /// Performs the measure operation.
    /// </summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="opsPerBlock">The ops per block.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="warmupOverride">The warmup override.</param>
    /// <returns>A task whose result is measure.</returns>
    private static async Task<Measurement> MeasureAsync(int blocks, int opsPerBlock, Func<ValueTask> operation, int? warmupOverride = null) {
        var warmup = warmupOverride ?? Math.Max(1, opsPerBlock / 10);
        for(var i = 0; i < warmup; i++) {
            await operation().ConfigureAwait(false);
        }

        var samples = new double[blocks];
        var allocationSamples = new double[blocks];
        for(var block = 0; block < blocks; block++) {
            var startAllocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for(var i = 0; i < opsPerBlock; i++) {
                await operation().ConfigureAwait(false);
            }

            var end = Stopwatch.GetTimestamp();
            var endAllocated = GC.GetAllocatedBytesForCurrentThread();
            samples[block] = (end - start) / (double)Stopwatch.Frequency * 1_000_000_000.0 / opsPerBlock;
            allocationSamples[block] = (endAllocated - startAllocated) / (double)opsPerBlock;
        }

        return new Measurement(samples, allocationSamples);
    }

    /// <summary>
    /// Runs with current ui culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="action">The action.</param>
    private static void RunWithCurrentUiCulture(string cultureName, Action action) {
        var original = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            action();
        }
        finally {
            CultureInfo.CurrentUICulture = original;
        }
    }

    /// <summary>
    /// Writes one structured performance result line for the current scenario.
    /// </summary>
    /// <param name="scenario">The scenario label.</param>
    /// <param name="keyCount">The number of catalog keys in the measurement.</param>
    /// <param name="opsPerBlock">The number of operations executed per measurement block.</param>
    /// <param name="blocks">The number of measurement blocks.</param>
    /// <param name="measurement">The collected latency and allocation samples.</param>
    private void EmitResult(string scenario, int keyCount, int opsPerBlock, int blocks, Measurement measurement) =>
        Emit(string.Format(
            CultureInfo.InvariantCulture,
            "RESULT | {0} | keys={1} | ops={2,6} x{3} | ns/op min={4,12:N1} median={5,12:N1} max={6,12:N1} | alloc median={7,12:N1} B/op",
            scenario,
            keyCount,
            opsPerBlock,
            blocks,
            measurement.MinNs,
            measurement.MedianNs,
            measurement.MaxNs,
            measurement.MedianAlloc));

    /// <summary>
    /// Performs the emit operation.
    /// </summary>
    /// <param name="line">The line.</param>
    private void Emit(string line) => TestContext?.WriteLine(line);

    /// <summary>
    /// Asserts measurement valid.
    /// </summary>
    /// <param name="measurement">The measurement.</param>
    private static void AssertMeasurementValid(Measurement measurement) =>
        Assert.IsTrue(
            double.IsFinite(measurement.MedianNs) && measurement.MedianNs > 0,
            "a performance measurement must contain positive finite samples; the harness or environment is broken");

    /// <summary>
    /// Represents a measurement.
    /// </summary>
    /// <param name="samples">The collected latency samples.</param>
    /// <param name="allocationSamples">The collected allocation samples.</param>
    private readonly struct Measurement(double[] samples, double[] allocationSamples) {
        /// <summary>
        /// Gets the min ns.
        /// </summary>
        public double MinNs => samples.Min();

        /// <summary>
        /// Gets the median ns.
        /// </summary>
        public double MedianNs => Median(samples);

        /// <summary>
        /// Gets the max ns.
        /// </summary>
        public double MaxNs => samples.Max();

        /// <summary>
        /// Gets the median alloc.
        /// </summary>
        public double MedianAlloc => Median(allocationSamples);

        /// <summary>
        /// Performs the median operation.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>The median value.</returns>
        private static double Median(double[] values) {
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            return sorted[sorted.Length / 2];
        }
    }
}
